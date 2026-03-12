using System.Text.Json;

public static class GettingDraftEndpoint
{
    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        ReceiptInteractionService receiptInteractionService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("GettingDraftEndpoint");

        try
        {
            var payload = await request.ReadFromJsonAsync<ReceiptDraftNotificationRequest>(cancellationToken);
            if (payload is null)
            {
                logger.LogWarning("Invalid getting_draft payload. Request body was empty or null.");
                return Results.BadRequest(ApiErrorResponse.InvalidRequest("draftId is required"));
            }

            var draftId = payload.ResolvedDraftId;
            logger.LogInformation("Getting draft request received. draftId={DraftId}", draftId);

            var validationMessage = ValidatePayload(payload);
            if (validationMessage is not null)
            {
                logger.LogWarning("Invalid getting_draft payload. draftId={DraftId} Message={Message}", draftId, validationMessage);
                return Results.BadRequest(ApiErrorResponse.InvalidRequest(validationMessage));
            }

            if (!HasDraftContent(payload))
            {
                logger.LogWarning("Draft not found. draftId={DraftId}", draftId);
                return Results.NotFound(ApiErrorResponse.DraftNotFound($"Draft not found for draftId '{draftId}'."));
            }

            var ocrItems = ReceiptItemMergeService.BuildOcrItems(payload.Items);
            var mergedItems = ReceiptItemMergeService.MergeForUi(ocrItems);

            logger.LogInformation(
                "Draft found. draftId={DraftId} RawItemCount={RawItemCount} MergedItemCount={MergedItemCount}",
                draftId,
                ocrItems.Count,
                mergedItems.Count);

            await receiptInteractionService.CreateOrUpdateSessionFromDraftAsync(payload, cancellationToken);

            return Results.Ok(new { message = "draft received" });
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid getting_draft payload. JSON deserialization failed.");
            return Results.BadRequest(ApiErrorResponse.InvalidRequest("Request body must be valid JSON."));
        }
        catch (BadHttpRequestException ex)
        {
            logger.LogWarning(ex, "Invalid getting_draft payload. Request body could not be read.");
            return Results.BadRequest(ApiErrorResponse.InvalidRequest("Request body is invalid."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while processing getting_draft request.");
            return Results.Json(
                ApiErrorResponse.UnexpectedServerError("An unexpected error occurred."),
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string? ValidatePayload(ReceiptDraftNotificationRequest payload)
    {
        var draftId = payload.ResolvedDraftId;
        if (string.IsNullOrWhiteSpace(draftId))
        {
            return "draftId is required";
        }

        if (!IsValidDraftId(draftId))
        {
            return "draftId is invalid";
        }

        return null;
    }

    private static bool HasDraftContent(ReceiptDraftNotificationRequest payload)
    {
        return !string.IsNullOrWhiteSpace(payload.BlobUrl) &&
               !string.IsNullOrWhiteSpace(payload.Status);
    }

    private static bool IsValidDraftId(string draftId)
    {
        if (draftId.Length > 128)
        {
            return false;
        }

        foreach (var ch in draftId)
        {
            if (char.IsLetterOrDigit(ch))
            {
                continue;
            }

            if (ch is '-' or '_' or ':' or '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
