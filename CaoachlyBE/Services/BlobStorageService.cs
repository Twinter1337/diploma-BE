using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using CaoachlyBE.Services.Interfaces;

namespace CaoachlyBE.Services;

public class BlobStorageService(IConfiguration configuration) : IBlobStorageService
{
    private readonly BlobServiceClient _client = new(configuration["AzureBlob:ConnectionString"]);

    public async Task<string> UploadAsync(Stream stream, string blobName, string contentType, string container)
    {
        var containerClient = _client.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType }, conditions: null);

        return blobClient.Uri.ToString();
    }

    public async Task DeleteAsync(string blobUrl, string container)
    {
        if (string.IsNullOrWhiteSpace(blobUrl)) return;
        var containerClient = _client.GetBlobContainerClient(container);
        var uri = new Uri(blobUrl);
        var prefix = $"/{container}/";
        var idx = uri.AbsolutePath.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return;
        var blobName = uri.AbsolutePath[(idx + prefix.Length)..];
        if (string.IsNullOrEmpty(blobName)) return;
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
    }

    public string GetReadSasUrl(string blobUrl, string container, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(blobUrl)) return blobUrl;

        var uri = new Uri(blobUrl);
        var prefix = $"/{container}/";
        var idx = uri.AbsolutePath.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return blobUrl;
        var blobName = uri.AbsolutePath[(idx + prefix.Length)..];
        if (string.IsNullOrEmpty(blobName)) return blobUrl;

        var blobClient = _client.GetBlobContainerClient(container).GetBlobClient(blobName);
        if (!blobClient.CanGenerateSasUri) return blobUrl;

        var builder = new BlobSasBuilder
        {
            BlobContainerName = container,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(ttl),
        };
        builder.SetPermissions(BlobSasPermissions.Read);
        return blobClient.GenerateSasUri(builder).ToString();
    }
}
