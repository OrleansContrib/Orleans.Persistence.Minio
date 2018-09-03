using Microsoft.Extensions.Logging;
using Minio;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Orleans.Persistence.Minio.Storage
{
    public class MinioStorage : IMinioStorage
    {
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _endpoint;
        private readonly string _containerPrefix;
        private readonly ILogger<MinioStorage> _logger;
        private readonly Stopwatch stopwwatch = new Stopwatch();

        public MinioStorage(ILogger<MinioStorage> logger, string accessKey, string secretKey, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(accessKey))
                throw new ArgumentException("Minio 'accessKey' is missing.");

            if (string.IsNullOrWhiteSpace(secretKey))
                throw new ArgumentException("Minio 'secretKey' is missing.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Minio 'endpoint' is missing.");

            _accessKey = accessKey;
            _secretKey = secretKey;
            _endpoint = endpoint;
            _logger = logger;
        }

        public MinioStorage(ILogger<MinioStorage> logger, string accessKey, string secretKey, string endpoint, string containerPrefix)
            : this(logger, accessKey, secretKey, endpoint)
        {
            if (string.IsNullOrWhiteSpace(containerPrefix))
                throw new ArgumentException("Minio 'containerPrefix' is missing.");

            _containerPrefix = containerPrefix;
        }

        private MinioClient CreateMinioClient() => new MinioClient(_endpoint, _accessKey, _secretKey);

        private string AppendPrefix(string prefix, string value) => string.IsNullOrEmpty(prefix) ? value : $"{prefix}-{value}";

        private string AppendContainerPrefix(string container) => string.IsNullOrEmpty(_containerPrefix) ? container : AppendPrefix(_containerPrefix, container);

        private (MinioClient client, string bucket, string objectName) GetStorage(string blobContainer, string blobPrefix, string blobName)
        {
            _logger.LogTrace("Creating Minio client: container={0} blobName={1} blobPrefix={2}", blobContainer, blobName, blobPrefix);
            stopwwatch.Restart();

            var client = CreateMinioClient();

            stopwwatch.Stop();
            _logger.LogTrace("Created Minio client: timems={0} container={0} blobName={1} blobPrefix={2}", stopwwatch.ElapsedMilliseconds, blobContainer, blobName, blobPrefix);

            return (client, AppendContainerPrefix(blobContainer), AppendPrefix(blobPrefix, blobName));
        }

        public Task<bool> ContainerExits(string blobContainer)
        {
            return CreateMinioClient().BucketExistsAsync(AppendContainerPrefix(blobContainer));
        }

        public Task CreateContainerAsync(string blobContainer)
        {
            return CreateMinioClient().MakeBucketAsync(blobContainer);
        }

        public async Task DeleteBlob(string blobContainer, string blobName, string blobPrefix = null)
        {
            var (client, bucket, objectName) =
                GetStorage(blobContainer, blobPrefix, blobName);

            _logger.LogTrace("Deleting blob: container={0} blobName={1} blobPrefix={2}", blobContainer, blobName, blobPrefix);
            stopwwatch.Restart();

            await client.RemoveObjectAsync(bucket, objectName);

            stopwwatch.Stop();
            _logger.LogTrace("Deleted blob: timems={0} container={0} blobName={1} blobPrefix={2}", stopwwatch.ElapsedMilliseconds, blobContainer, blobName, blobPrefix);
        }

        public async Task<Stream> ReadBlob(string blobContainer, string blobName, string blobPrefix = null)
        {
            var (client, bucket, objectName) =
                GetStorage(blobContainer, blobPrefix, blobName);

            _logger.LogTrace("Reading blob: container={0} blobName={1} blobPrefix={2}", blobContainer, blobName, blobPrefix);
            stopwwatch.Restart();

            var ms = new MemoryStream();
            await client.GetObjectAsync(bucket, objectName, stream =>
            {
                stream.CopyTo(ms);
            });

            stopwwatch.Stop();
            _logger.LogTrace("Read blob: timems={0} container={0} blobName={1} blobPrefix={2}", stopwwatch.ElapsedMilliseconds, blobContainer, blobName, blobPrefix);

            ms.Position = 0;
            return ms;
        }

        public async Task UploadBlob(string blobContainer, string blobName, Stream blob, string blobPrefix = null, string contentType = null)
        {
            var (client, container, name) =
                GetStorage(blobContainer, blobPrefix, blobName);

            _logger.LogTrace("Writing blob: container={0} blobName={1} blobPrefix={2}", blobContainer, blobName, blobPrefix);
            stopwwatch.Restart();

            await client.PutObjectAsync(container, name, blob, blob.Length, contentType: contentType);
            stopwwatch.Stop();
            _logger.LogTrace("Wrote blob: timems={0} container={0} blobName={1} blobPrefix={2}", stopwwatch.ElapsedMilliseconds, blobContainer, blobName, blobPrefix);
        }
    }
}
