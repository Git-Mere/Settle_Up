using System.Globalization;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Core;
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
    private readonly TokenCredential _defaultAzureCredential = new DefaultAzureCredential();

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
            ? new DocumentIntelligenceClient(endpoint, _defaultAzureCredential)
            : new DocumentIntelligenceClient(endpoint, new AzureKeyCredential(_options.DocumentIntelligenceApiKey));
    }

    public async Task<ParsedReceiptResult> ParseFromBlobAsync(string blobUrl, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt_parser.document_intelligence.parse");
        activity?.SetTag("blob.url", blobUrl);
        _logger.LogInformation("Receipt parsing started. SourceType=blob BlobUrl={BlobUrl}", blobUrl);

        var blobClient = new BlobClient(new Uri(blobUrl), _defaultAzureCredential);
        try
        {
            var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
            return await ParseFromBinaryAsync(downloadResult.Value.Content, blobUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Receipt blob download failed. BlobUrl={BlobUrl}", blobUrl);
            throw;
        }
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
        var tipField = TryGetField(analyzedDocument, "Tip");
        var totalField = TryGetField(analyzedDocument, "Total");
        var dateField = TryGetField(analyzedDocument, "TransactionDate");

        var merchantName = merchantField?.Content;
        var subtotal = TryParseDecimal(subtotalField);
        var totalTax = TryParseDecimal(taxField);
        var taxBreakdown = ExtractTaxBreakdown(analyzedDocument, totalTax);
        var tip = TryParseDecimal(tipField);
        var total = TryParseDecimal(totalField);
        var transactionDate = TryParseDate(dateField?.Content);
        var itemExtraction = ExtractItems(analyzedDocument);
        var normalizedSubtotal = itemExtraction.Items.Count > 0
            ? decimal.Round(
                itemExtraction.Items.Sum(item => item.TotalPrice ?? item.UnitPrice ?? 0m),
                2,
                MidpointRounding.AwayFromZero)
            : subtotal;
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
            Subtotal: normalizedSubtotal,
            Tax: taxBreakdown.GeneralTax,
            Sst: taxBreakdown.Sst,
            Slt: taxBreakdown.Slt,
            Tip: tip,
            UnattributedDiscount: itemExtraction.UnattributedDiscount,
            Total: total,
            ParseMetadata: new ParseMetadata(
                ModelId: _options.ModelId,
                MerchantConfidence: merchantField?.Confidence,
                TotalConfidence: totalField?.Confidence),
            Items: itemExtraction.Items);
    }

    private static TaxBreakdown ExtractTaxBreakdown(AnalyzedDocument? document, decimal? totalTax)
    {
        var taxDetailsField = TryGetField(document, "TaxDetails");
        if (taxDetailsField?.ValueList is null || taxDetailsField.ValueList.Count == 0)
        {
            return new TaxBreakdown(totalTax, null, null);
        }

        var generalTax = 0m;
        var sst = 0m;
        var slt = 0m;
        var classifiedAny = false;

        foreach (var taxDetailField in taxDetailsField.ValueList)
        {
            if (taxDetailField?.ValueDictionary is null)
            {
                continue;
            }

            taxDetailField.ValueDictionary.TryGetValue("Description", out var descriptionField);
            taxDetailField.ValueDictionary.TryGetValue("Amount", out var amountField);

            var amount = TryParseDecimal(amountField) ?? 0m;
            if (amount <= 0m)
            {
                continue;
            }

            classifiedAny = true;
            switch (ClassifyTaxDetail(descriptionField?.Content))
            {
                case TaxKind.Sst:
                    sst += amount;
                    break;
                case TaxKind.Slt:
                    slt += amount;
                    break;
                default:
                    generalTax += amount;
                    break;
            }
        }

        if (!classifiedAny)
        {
            return new TaxBreakdown(totalTax, null, null);
        }

        if (totalTax is decimal receiptTotalTax)
        {
            var classifiedTotal = generalTax + sst + slt;
            var remainder = decimal.Round(receiptTotalTax - classifiedTotal, 2, MidpointRounding.AwayFromZero);
            if (remainder > 0m)
            {
                generalTax += remainder;
            }
        }

        return new TaxBreakdown(generalTax, sst == 0m ? null : sst, slt == 0m ? null : slt);
    }

    private static TaxKind ClassifyTaxDetail(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return TaxKind.General;
        }

        var normalized = description.Trim().ToLowerInvariant();
        if (normalized.Contains("slt", StringComparison.Ordinal) ||
            normalized.Contains("spirits liter", StringComparison.Ordinal) ||
            normalized.Contains("spirit liter", StringComparison.Ordinal) ||
            normalized.Contains("liter tax", StringComparison.Ordinal) ||
            normalized.Contains("litre tax", StringComparison.Ordinal))
        {
            return TaxKind.Slt;
        }

        if (normalized.Contains("sst", StringComparison.Ordinal) ||
            normalized.Contains("spirits sales", StringComparison.Ordinal) ||
            normalized.Contains("spirit sales", StringComparison.Ordinal))
        {
            return TaxKind.Sst;
        }

        return TaxKind.General;
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

    private static ItemExtractionResult ExtractItems(AnalyzedDocument? document)
    {
        var itemsField = TryGetField(document, "Items");
        if (itemsField?.ValueList is null)
        {
            return new ItemExtractionResult([], null);
        }

        var rawItems = new List<ParsedReceiptItem>();
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

            rawItems.Add(item);
        }

        return NormalizeItems(rawItems);
    }

    private static ItemExtractionResult NormalizeItems(IReadOnlyList<ParsedReceiptItem> rawItems)
    {
        if (rawItems.Count == 0)
        {
            return new ItemExtractionResult([], null);
        }

        var normalizedItems = new List<ParsedReceiptItem>();

        foreach (var rawItem in rawItems)
        {
            if (!TryResolveDiscountAmount(rawItem, out var discountAmount))
            {
                normalizedItems.Add(rawItem);
                continue;
            }

            if (normalizedItems.Count == 0)
            {
                continue;
            }

            var previousItem = normalizedItems[^1];
            var adjustedItem = TryApplyDiscount(previousItem, discountAmount);
            if (adjustedItem is null)
            {
                continue;
            }

            normalizedItems[^1] = adjustedItem;
        }

        return new ItemExtractionResult(normalizedItems, null);
    }

    private static bool TryResolveDiscountAmount(ParsedReceiptItem item, out decimal discountAmount)
    {
        discountAmount = 0m;

        var totalPrice = item.TotalPrice;
        var unitPrice = item.UnitPrice;
        var quantity = item.Quantity;
        var description = item.Description?.Trim();

        if (totalPrice is decimal negativeTotal && negativeTotal < 0m)
        {
            discountAmount = decimal.Abs(negativeTotal);
            return true;
        }

        if (unitPrice is decimal negativeUnit && negativeUnit < 0m)
        {
            var multiplier = quantity is decimal q && q > 0m ? q : 1m;
            discountAmount = decimal.Abs(negativeUnit * multiplier);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(description) &&
            LooksLikeDiscountDescription(description) &&
            ((totalPrice is decimal zeroTotal && zeroTotal == 0m) ||
             (unitPrice is decimal zeroUnit && zeroUnit == 0m)))
        {
            discountAmount = 0m;
            return true;
        }

        return false;
    }

    private static bool LooksLikeDiscountDescription(string description)
    {
        var normalized = description.Trim().ToLowerInvariant();
        return normalized.Contains("discount", StringComparison.Ordinal) ||
               normalized.Contains("coupon", StringComparison.Ordinal) ||
               normalized.Contains("promo", StringComparison.Ordinal) ||
               normalized.Contains("saving", StringComparison.Ordinal) ||
               normalized.Contains("rebate", StringComparison.Ordinal) ||
               normalized.Contains("off ", StringComparison.Ordinal) ||
               normalized.EndsWith(" off", StringComparison.Ordinal);
    }

    private static ParsedReceiptItem? TryApplyDiscount(ParsedReceiptItem item, decimal discountAmount)
    {
        if (discountAmount <= 0m)
        {
            return item;
        }

        var quantity = item.Quantity is decimal rawQuantity && rawQuantity > 0m ? rawQuantity : 1m;
        var originalTotal = item.OriginalTotalPrice ?? item.TotalPrice ?? item.UnitPrice;
        if (originalTotal is null || originalTotal <= 0m)
        {
            return null;
        }

        var originalUnit = item.OriginalUnitPrice ?? item.UnitPrice ?? decimal.Round(originalTotal.Value / quantity, 2, MidpointRounding.AwayFromZero);
        var adjustedTotal = decimal.Round(originalTotal.Value - discountAmount, 2, MidpointRounding.AwayFromZero);
        if (adjustedTotal < 0m)
        {
            return null;
        }

        var adjustedUnit = quantity > 0m
            ? decimal.Round(adjustedTotal / quantity, 2, MidpointRounding.AwayFromZero)
            : adjustedTotal;
        var accumulatedDiscount = (item.DiscountAmount ?? 0m) + discountAmount;

        return item with
        {
            UnitPrice = adjustedUnit,
            TotalPrice = adjustedTotal,
            OriginalUnitPrice = originalUnit,
            OriginalTotalPrice = originalTotal.Value,
            DiscountAmount = decimal.Round(accumulatedDiscount, 2, MidpointRounding.AwayFromZero)
        };
    }

    private sealed record TaxBreakdown(decimal? GeneralTax, decimal? Sst, decimal? Slt);
    private sealed record ItemExtractionResult(IReadOnlyList<ParsedReceiptItem> Items, decimal? UnattributedDiscount);

    private enum TaxKind
    {
        General,
        Sst,
        Slt
    }
}
