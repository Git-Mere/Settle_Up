using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Discord;

sealed class BlobImageUploader
{
    private readonly BlobContainerClient _containerClient;
    private readonly HttpClient _httpClient;

    private BlobImageUploader(BlobContainerClient containerClient, HttpClient httpClient)
    {
        _containerClient = containerClient;
        _httpClient = httpClient;
    }

    public static BlobImageUploader? CreateFromEnvironment(HttpClient httpClient, out string error)
    {
        var containerName = Environment.GetEnvironmentVariable("AZURE_BLOB_CONTAINER_NAME");
        if (string.IsNullOrWhiteSpace(containerName))
        {
            error = "AZURE_BLOB_CONTAINER_NAME 환경 변수가 필요합니다.";
            return null;
        }

        var connectionString = Environment.GetEnvironmentVariable("AZURE_BLOB_CONNECTION_STRING");
        var accountUrl = Environment.GetEnvironmentVariable("AZURE_BLOB_ACCOUNT_URL");

        try
        {
            BlobContainerClient containerClient;

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                containerClient = new BlobContainerClient(connectionString, containerName);
            }
            else if (!string.IsNullOrWhiteSpace(accountUrl))
            {
                var serviceClient = new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential());
                containerClient = serviceClient.GetBlobContainerClient(containerName);
            }
            else
            {
                error = "AZURE_BLOB_CONNECTION_STRING 또는 AZURE_BLOB_ACCOUNT_URL 중 하나가 필요합니다.";
                return null;
            }

            error = string.Empty;
            return new BlobImageUploader(containerClient, httpClient);
        }
        catch (Exception ex)
        {
            error = $"Blob 클라이언트 초기화 실패: {ex.Message}";
            return null;
        }
    }

    public async Task<BlobUploadResult> UploadReceiptImageAsync(IAttachment attachment, ulong userId, CancellationToken cancellationToken = default)
    {
        if (!TryResolveImageMetadata(attachment, out var extension, out var contentType))
        {
            throw new InvalidOperationException("jpg/jpeg/png 파일만 업로드할 수 있습니다.");
        }

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"{DateTime.UtcNow:yyyy/MM/dd}/{userId}/{Guid.NewGuid():N}{extension}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        using var response = await _httpClient.GetAsync(attachment.Url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        await blobClient.UploadAsync(
            body,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            },
            cancellationToken);

        return new BlobUploadResult(
            ContainerName: _containerClient.Name,
            BlobName: blobName,
            BlobUri: blobClient.Uri.ToString());
    }

    private static bool TryResolveImageMetadata(IAttachment attachment, out string extension, out string contentType)
    {
        extension = string.Empty;
        contentType = string.Empty;

        var filenameExtension = Path.GetExtension(attachment.Filename).ToLowerInvariant();
        var normalizedContentType = attachment.ContentType?.Trim().ToLowerInvariant();

        var isSupportedByExtension = filenameExtension is ".jpg" or ".jpeg" or ".png";
        var isSupportedByContentType = normalizedContentType is "image/jpg" or "image/jpeg" or "image/png";

        if (!isSupportedByExtension && !isSupportedByContentType)
        {
            return false;
        }

        extension = filenameExtension switch
        {
            ".jpg" or ".jpeg" => ".jpg",
            ".png" => ".png",
            _ => normalizedContentType switch
            {
                "image/jpg" or "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => ".jpg"
            }
        };

        contentType = extension == ".png" ? "image/png" : "image/jpeg";
        return true;
    }
}

record BlobUploadResult(string ContainerName, string BlobName, string BlobUri);
