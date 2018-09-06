# Orleans.Persistence.Minio

Minio implementation of Orleans Grain Storage Provider

[![Build status](https://ci.appveyor.com/api/projects/status/du87opx8tcyp7mda?svg=true)](https://ci.appveyor.com/project/Kimserey16189/orleans-persistence-minio-bopb3)
[![NuGet](https://img.shields.io/nuget/v/Orleans.Persistence.Minio.svg?style=flat&colorB=blue)](http://www.nuget.org/packages/Orleans.Persistence.Minio)

[Minio](https://www.minio.io/) is a open source cloud storage.

## Installation

Install `Orleans.Persistence.Minio` NuGet package in the project containing your `SiloHost` definition with the following command line:

```
PM> Install-Package Orleans.Persistence.Minio
```

## Usage

Import the extensions: 

```
using Orleans.Persistence.Minio
```

Register the `Orleans.Persistence.Minio` grain storage on the `ISiloHostBuilder`:

```
ISiloHostBuilder builder = new SiloHostBuilder()
	.AddMinioGrainStorage("minio", options =>
		{
			options.AccessKey = "minio_access_key";
			options.SecretKey = "minio_secret_key";
			options.Endpoint = "localhost:9000";
			options.Container = "grain-storage";
		}
	);
```

The `MinioGrainStorageOptions` is used to specify the following:

- `AccessKey`: Minio access key
- `SecretKey`: Minio secret key
- `Endpoint`: Minio endpoint
- `Container`: The container under which the grain storage will be stored

Then use the storage on grains:

```
[StorageProvider(ProviderName = "minio")]
public class MySuperGrain : Grain<MySuperGrainState>, IMySuperGrain
{ ... }
```

## License 

MIT