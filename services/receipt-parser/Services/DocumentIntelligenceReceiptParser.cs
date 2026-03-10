using System.Globalization;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using receipt_parser.Configuration;
using receipt_parser.Models;
using receipt_parser.Observability;

namespace receipt_parser.Services;

public sealed class DocumentIntelligenceReceiptParser
{
    private readonly ReceiptParserOptions _options;
    private readonly ILogger<DocumentIntelligenceReceiptParser> _logger;
    private readonly DocumentIntelligenceClient _documentClient;

    public DocumentIntelligenceReceiptParser(
        IOptions<ReceiptParserOptions> options,
        ILogger<DocumentIntelligenceReceiptParser> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.DocumentIntelligenceEndpoint))
        {
            throw new InvalidOperationException("ReceiptParser:DocumentIntelligenceEndpoint 설정이 필요합니다.");
        }

        var endpoint = new Uri(_options.DocumentIntelligenceEndpoint);
        _documentClient = string.IsNullOrWhiteSpace(_options.DocumentIntelligenceApiKey)
            ? new DocumentIntelligenceClient(endpoint, new DefaultAzureCredential())
            : new DocumentIntelligenceClient(endpoint, new AzureKeyCredential(_options.DocumentIntelligenceApiKey));
    }

    public async Task<ParsedReceiptResult> ParseFromBlobAsync(string blobUrl, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt_parser.document_intelligence.parse");
        activity?.SetTag("blob.url", blobUrl);
        _logger.LogInformation("Receipt parsing started. SourceType=blob BlobUrl={BlobUrl}", blobUrl);

        var blobClient = new BlobClient(new Uri(blobUrl), new DefaultAzureCredential());
        await using var stream = new MemoryStream();
        try
        {
            await blobClient.DownloadToAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Receipt blob download failed. BlobUrl={BlobUrl}", blobUrl);
            throw;
        }

        stream.Position = 0;

        var binaryData = BinaryData.FromStream(stream);
        return await ParseFromBinaryAsync(binaryData, blobUrl, cancellationToken);
    }

    public async Task<ParsedReceiptResult> ParseFromBinaryAsync(
        BinaryData binaryData,
        string source,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt_parser.document_intelligence.parse_binary");
        activity?.SetTag("receipt.source", source);

        Operation<AnalyzeResult> operation;
        try
        {
            operation = await _documentClient.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                _options.ModelId,
                binaryData,
                cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Receipt parsing failed. Source={Source}", source);
            throw;
        }

        var result = operation.Value;

        var analyzedDocument = result.Documents.FirstOrDefault();
        var merchantField = TryGetField(analyzedDocument, "MerchantName");
        var subtotalField = TryGetField(analyzedDocument, "Subtotal");
        var taxField = TryGetField(analyzedDocument, "TotalTax");
        var totalField = TryGetField(analyzedDocument, "Total");
        var dateField = TryGetField(analyzedDocument, "TransactionDate");

        var merchantName = merchantField?.Content;
        var subtotal = TryParseDecimal(subtotalField);
        var tax = TryParseDecimal(taxField);
        var total = TryParseDecimal(totalField);
        var transactionDate = TryParseDate(dateField?.Content);
        var items = ExtractItems(analyzedDocument);
        var currency = TryParseCurrencyCode(totalField);

        var receiptId = Guid.NewGuid().ToString("N");
        _logger.LogInformation(
            "Receipt parsing completed. ReceiptId={ReceiptId} Source={Source} MerchantName={MerchantName} Total={Total}",
            receiptId,
            source,
            merchantName,
            total);

        return new ParsedReceiptResult(
            ReceiptId: receiptId,
            BlobUrl: source,
            MerchantName: merchantName,
            Currency: currency,
            TransactionDate: transactionDate,
            Subtotal: subtotal,
            Tax: tax,
            Total: total,
            ParseMetadata: new ParseMetadata(
                ModelId: _options.ModelId,
                MerchantConfidence: merchantField?.Confidence,
                TotalConfidence: totalField?.Confidence),
            Items: items);
    }

    private static DocumentField? TryGetField(AnalyzedDocument? document, string fieldName)
    {
        if (document?.Fields is null)
        {
            return null;
        }

        return document.Fields.TryGetValue(fieldName, out var field) ? field : null;
    }

    private static decimal? TryParseDecimal(DocumentField? field)
    {
        if (field is null)
        {
            return null;
        }

        if (field.ValueCurrency is not null)
        {
            return Convert.ToDecimal(field.ValueCurrency.Amount, CultureInfo.InvariantCulture);
        }

        if (field.ValueDouble.HasValue)
        {
            return Convert.ToDecimal(field.ValueDouble.Value, CultureInfo.InvariantCulture);
        }

        if (field.ValueInt64.HasValue)
        {
            return field.ValueInt64.Value;
        }

        return decimal.TryParse(field.Content, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateOnly? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static string? TryParseCurrencyCode(DocumentField? amountField)
    {
        if (amountField is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(amountField.ValueCurrency?.CurrencyCode))
        {
            return amountField.ValueCurrency.CurrencyCode;
        }

        if (amountField.Content?.Contains('$') == true)
        {
            return "USD";
        }

        return null;
    }

    private static IReadOnlyList<ParsedReceiptItem> ExtractItems(AnalyzedDocument? document)
    {
        var itemsField = TryGetField(document, "Items");
        if (itemsField?.ValueList is null)
        {
            return [];
        }

        var items = new List<ParsedReceiptItem>();
        for (var index = 0; index < itemsField.ValueList.Count; index++)
        {
            var itemField = itemsField.ValueList[index];
            if (itemField?.ValueDictionary is null)
            {
                continue;
            }

            itemField.ValueDictionary.TryGetValue("Description", out var descriptionField);
            itemField.ValueDictionary.TryGetValue("Quantity", out var quantityField);
            itemField.ValueDictionary.TryGetValue("UnitPrice", out var unitPriceField);
            itemField.ValueDictionary.TryGetValue("TotalPrice", out var totalPriceField);

            var item = new ParsedReceiptItem(
                Id: $"item{index + 1}",
                Description: descriptionField?.Content,
                Quantity: TryParseDecimal(quantityField),
                UnitPrice: TryParseDecimal(unitPriceField),
                TotalPrice: TryParseDecimal(totalPriceField));

            items.Add(item);
        }

        return items;
    }
}
