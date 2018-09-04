# Orleans.Persistence.Minio

Minio implementation of Orleans Grain Storage Provider

[![Build status](https://ci.appveyor.com/api/projects/status/9h5jrxc0a5uq813x?svg=true)](https://ci.appveyor.com/project/Kimserey16189/orleans-persistence-minio)
[![NuGet](https://img.shields.io/nuget/v/Orleans.Persistence.Minio.svg?style=flat&colorB=blue)](http://www.nuget.org/packages/Orleans.Persistence.Minio)

## Installation

Using Nuget command line:

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
ISiloHostBuilder builder = 
	new SiloHostBuilder()
		.AddMinioGrainStorage("default", options =>
			{
				options.AccessKey = "minio_access_key";
				options.SecretKey = "minio_secret_key";
				options.Endpoint = "minio_endpoint";
				options.Container = "container prefix";
			}
		);
```

## License 

MIT