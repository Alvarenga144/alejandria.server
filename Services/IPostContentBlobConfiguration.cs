using Azure.Storage.Blobs;

namespace Alejandria.Server.Services
{
    public interface IPostContentBlobConfiguration
    {
        bool DeleteBlob(string blobName, string containerName);

        BlobContainerClient GetContainer(string containerName);

        Task<string> UploadBlobAsync(string file, string blobName);
    }
}
