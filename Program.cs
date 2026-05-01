using Azure.Identity;
using CameraEvent.Model;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .Configure<AzureServiceMsiOptions>("CosmosDBOptions", builder.Configuration.GetSection("CosmosDBConnection"))
    .Configure<AzureServiceMsiOptions>("StorageOptions", builder.Configuration.GetSection("CosmosDBConnection"))
    .AddSingleton(sp =>
    {
        var cosmosMsiOptions = builder.Configuration
            .GetSection("CosmosMsiOptions")
            .Get<AzureServiceMsiOptions>();
        
        var credential = new ManagedIdentityCredential(cosmosMsiOptions.ClientId);
        return new CosmosClient(cosmosMsiOptions.Endpoint, credential);
    })
    .AddAzureClients(cBuilder =>
    {
        var storageMsiOptions = builder.Configuration
            .GetSection("StorageMsiOptions")
            .Get<AzureServiceMsiOptions>();

        var storageUri = new Uri(storageMsiOptions.Endpoint);
        cBuilder.AddBlobServiceClient(storageUri).WithCredential(new DefaultAzureCredential(
            new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = storageMsiOptions.ClientId
            }
        ));
    });

builder.Build().Run();
