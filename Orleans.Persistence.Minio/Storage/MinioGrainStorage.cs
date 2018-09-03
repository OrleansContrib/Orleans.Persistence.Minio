using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio.Exceptions;
using Newtonsoft.Json;
using Orleans.Runtime;
using Orleans.Serialization;
using Orleans.Storage;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Orleans.Persistence.Minio.Storage
{
    public partial class MinioGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>
    {
        private readonly string _name;
        private readonly string _container;
        private readonly ILogger<MinioGrainStorage> _logger;
        private readonly IMinioStorage _storage;
        private readonly IGrainFactory _grainFactory;
        private readonly ITypeResolver _typeResolver;
        private JsonSerializerSettings _jsonSettings;

        public MinioGrainStorage(string name, string container, IMinioStorage storage, ILogger<MinioGrainStorage> logger, IGrainFactory grainFactory, ITypeResolver typeResolver)
        {
            _name = name;
            _container = container;
            _logger = logger;
            _storage = storage;
            _grainFactory = grainFactory;
            _typeResolver = typeResolver;
        }

        public async Task ClearStateAsync(string grainType, GrainReference grainReference, IGrainState grainState)
        {
            string blobName = GetBlobNameString(grainType, grainReference);

            try
            {
                _logger.LogTrace("Clearing: GrainType={0} Grainid={1} ETag={2} to BlobName={3} in Container={4}",
                    grainType, grainReference, grainState.ETag, blobName, _container);

                await _storage.DeleteBlob(_container, blobName);
                grainState.ETag = null;

                _logger.LogTrace("Cleared: GrainType={0} Grainid={1} ETag={2} to BlobName={3} in Container={4}",
                    grainType, grainReference, grainState.ETag, blobName, _container);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing: GrainType={0} Grainid={1} ETag={2} BlobName={3} in Container={4} Exception={5}",
                    grainType, grainReference, grainState.ETag, blobName, _container, ex.Message);

                throw;
            }
        }

        public async Task ReadStateAsync(string grainType, GrainReference grainReference, IGrainState grainState)
        {
            string blobName = GetBlobNameString(grainType, grainReference);

            try
            {
                _logger.LogTrace("Reading: GrainType={0} Grainid={1} ETag={2} to BlobName={3} in Container={4}",
                    grainType, grainReference, grainState.ETag, blobName, _container);

                GrainStateRecord record;
                try
                {
                    using (var blob = await _storage.ReadBlob(_container, blobName))
                    using (var stream = new MemoryStream())
                    {
                        await blob.CopyToAsync(stream);
                        record = ConvertFromStorageFormat(stream.ToArray());
                    }
                }
                catch (BucketNotFoundException ex)
                {
                    _logger.LogTrace("ContainerNotFound reading: GrainType={0} Grainid={1} ETag={2} to BlobName={3} in Container={4} Exception={5}",
                        grainType, grainReference, grainState.ETag, blobName, _container, ex.message);

                    return;
                }
                catch (ObjectNotFoundException ex)
                {
                    _logger.LogTrace("BlobNotFound reading: GrainType={0} Grainid={1} ETag={2} to BlobName={3} in Container={4} Exception={5}",
                        grainType, grainReference, grainState.ETag, blobName, _container, ex.message);

                    return;
                }

                grainState.State = record.State;
                grainState.ETag = record.ETag.ToString();

                _logger.LogTrace("Read: GrainType={0} Grainid={1} ETag={2} to BlobName={3} in Container={4}",
                    grainType, grainReference, grainState.ETag, blobName, _container);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading: GrainType={0} Grainid={1} ETag={2} from BlobName={3} in Container={4} Exception={5}",
                    grainType, grainReference, grainState.ETag, blobName, _container, ex.Message);

                throw;
            }
        }

        public async Task WriteStateAsync(string grainType, GrainReference grainReference, IGrainState grainState)
        {
            string blobName = GetBlobNameString(grainType, grainReference);

            int newETag = string.IsNullOrEmpty(grainState.ETag) ? 0 : Int32.Parse(grainState.ETag) + 1;
            try
            {
                _logger.LogTrace("Writing: GrainType={0} Grainid={1} ETag={2} to BlobName={3} in Container={4}",
                    grainType, grainReference, grainState.ETag, blobName, _container);


                var record = new GrainStateRecord
                {
                    ETag = newETag,
                    State = grainState.State
                };

                using (var stream = new MemoryStream(ConvertToStorageFormat(record)))
                {
                    await _storage.UploadBlob(_container, blobName, stream, contentType: "application/json");
                }

                grainState.ETag = newETag.ToString();

                _logger.LogTrace("Wrote: GrainType={0} Grainid={1} ETag={2} to BlobName={3} in Container={4}",
                    grainType, grainReference, grainState.ETag, blobName, _container);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing: GrainType={0} Grainid={1} ETag={2} from BlobName={3} in Container={4} Exception={5}",
                    grainType, grainReference, grainState.ETag, blobName, _container, ex.Message);

                throw;
            }
        }

        private string GetBlobNameString(string grainType, GrainReference grainReference)
        {
            return $"{grainType}-{grainReference.ToKeyString()}";
        }

        private byte[] ConvertToStorageFormat(object record)
        {
            var data = JsonConvert.SerializeObject(record, _jsonSettings);
            return Encoding.UTF8.GetBytes(data);
        }

        private GrainStateRecord ConvertFromStorageFormat(byte[] content)
        {
            var json = Encoding.UTF8.GetString(content);
            var record = JsonConvert.DeserializeObject<GrainStateRecord>(json, _jsonSettings);
            return record;
        }

        private async Task Init(CancellationToken ct)
        {
            _jsonSettings = OrleansJsonSerializer.UpdateSerializerSettings(OrleansJsonSerializer.GetDefaultSerializerSettings(_typeResolver, _grainFactory), false, false, null);

            if (!await _storage.ContainerExits(_container))
            {
                await _storage.CreateContainerAsync(_container);
            }
        }

        public void Participate(ISiloLifecycle lifecycle)
        {
            lifecycle.Subscribe(OptionFormattingUtilities.Name<MinioGrainStorage>(_name), ServiceLifecycleStage.ApplicationServices, Init);
        }
    }

    public static class MinioGrainStorageFactory
    {
        public static IGrainStorage Create(IServiceProvider services, string name)
        {
            IOptionsSnapshot<MinioGrainStorageOptions> optionsSnapshot = services.GetRequiredService<IOptionsSnapshot<MinioGrainStorageOptions>>();
            var options = optionsSnapshot.Get(name);
            IMinioStorage storage = ActivatorUtilities.CreateInstance<MinioStorage>(services, options.AccessKey, options.SecretKey, options.Endpoint);
            return ActivatorUtilities.CreateInstance<MinioGrainStorage>(services, name, options.Container, storage);
        }
    }
}
