using Microsoft.Extensions.Logging;

sealed class BlobUploaderProvider
{
    public BlobUploaderProvider(IHttpClientFactory httpClientFactory, ILogger<BlobUploaderProvider> logger)
    {
        Uploader = BlobImageUploader.CreateFromEnvironment(httpClientFactory.CreateClient(nameof(BlobImageUploader)), logger, out var error);
        InitializationError = error;

        if (Uploader is null)
        {
            logger.LogWarning("Blob uploader is disabled. Reason={Reason}", error);
        }
        else
        {
            logger.LogInformation("Blob uploader is enabled.");
        }
    }

    public BlobImageUploader? Uploader { get; }
    public string? InitializationError { get; }
}
