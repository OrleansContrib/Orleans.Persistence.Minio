using System.IO;
using System.Threading.Tasks;

namespace Orleans.Persistence.Minio.Storage
{
    public interface IMinioStorage
    {
        Task<bool> ContainerExits(string blobContainer);
        Task CreateContainerAsync(string blobContainer);
        Task<Stream> ReadBlob(string blobContainer, string blobName, string blobPrefix = null);
        Task UploadBlob(string blobContainer, string blobName, Stream blob, string blobPrefix = null, string contentType = null);
        Task DeleteBlob(string blobContainer, string blobName, string blobPrefix = null);
    }
}
