namespace CaoachlyBE.Services.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream stream, string blobName, string contentType, string container);
    Task DeleteAsync(string blobUrl, string container);
    string GetReadSasUrl(string blobUrl, string container, TimeSpan ttl);
}
