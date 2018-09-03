using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Persistence.Minio.Storage;
using Orleans.Runtime;
using Orleans.Storage;
using System;

namespace Orleans.Persistence.Minio.Hosting
{
    public static class MinioSiloBuilderExtensions
    {
        public static ISiloHostBuilder AddMinioGrainStorage(this ISiloHostBuilder builder, string providerName, Action<MinioGrainStorageOptions> options)
        {
            return builder.ConfigureServices(services => services.AddMinioGrainStorage(providerName, ob => ob.Configure(options)));
        }

        public static IServiceCollection AddMinioGrainStorage(this IServiceCollection services, string providerName, Action<OptionsBuilder<MinioGrainStorageOptions>> options)
        {
            options?.Invoke(services.AddOptions<MinioGrainStorageOptions>(providerName));
            return services
                .AddSingletonNamedService(providerName, MinioGrainStorageFactory.Create)
                .AddSingletonNamedService(providerName, (s, n) => (ILifecycleParticipant<ISiloLifecycle>)s.GetRequiredServiceByName<IGrainStorage>(n));
        }
    }
}
