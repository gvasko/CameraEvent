using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Security.Policy;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using CameraEvent.Model;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CameraEvent.Function;

public class CreateBlobVideoTags
{
    private readonly ILogger<CreateBlobVideoTags> _logger;
    private readonly Container _cosmosContainer;
    private readonly BlobContainerClient _blobContainerClient;

    public CreateBlobVideoTags(IConfiguration config, CosmosClient cosmosClient, BlobServiceClient blobServiceClient, ILogger<CreateBlobVideoTags> logger)
    {
        _logger = logger;
        _cosmosContainer = cosmosClient.GetContainer(config["CosmosDatabaseName"], config["CosmosContainerName"]);
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(config["CameraVideoBlobContainerName"]);

    }

    [Function(nameof(CreateBlobVideoTags))]
    public async Task Run([BlobTrigger("%CameraVideoBlobContainerName%/%CameraVideoPath%{name}", Source = BlobTriggerSource.EventGrid, Connection = "CameraVideoStorageConnection")] Stream stream, string name)
    {
        try
        {
            _logger.LogInformation("Blob Trigger (using Event Grid) processed blob, name: {name}", name);

            if (_cosmosContainer == null)
            {
                throw new Exception("CosmosDB container is not initialized. Check configuration.");
            }

            var blobNameComponents = name.Split('_');

            if (blobNameComponents.Length < 3)
            {
                throw new Exception($"Invalid blob name: {name}");
            }

            var cosmosId = blobNameComponents[1];
            var cosmosPartitionKey = new PartitionKey(blobNameComponents[2]);

            using var response = await _cosmosContainer.ReadItemStreamAsync(cosmosId, cosmosPartitionKey);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception($"Cosmos item not found: {cosmosId}, {cosmosPartitionKey}");
            }
            else if (response.IsSuccessStatusCode)
            {
                var videoMetadata = await JsonSerializer.DeserializeAsync<ObjectDetectionMetaDataDto>(response.Content);
                _logger.LogInformation("Found metadata in cosmos: {metadata}", videoMetadata);

                if (videoMetadata == null)
                {
                    _logger.LogInformation("Video metadata not received.");
                    return;    
                }

                if (videoMetadata.IsProcessed)
                {
                    _logger.LogInformation("Already processed.");
                    return;    
                }

                var tags = new Dictionary<string, string>();

                var objectTypeTagValue = videoMetadata.ObjectType;
                if (!string.IsNullOrEmpty(objectTypeTagValue))
                {
                    tags["ObjectType"] = objectTypeTagValue;
                }

                var cameraTagValue = videoMetadata.CameraId;
                if (!string.IsNullOrEmpty(cameraTagValue))
                {
                    tags["Camera"] = cameraTagValue;
                }
                
                var startTimeTagValue = videoMetadata.EventStartTime;
                if (!string.IsNullOrEmpty(startTimeTagValue))
                {
                    var parsed = double.TryParse(startTimeTagValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var timestampDouble);
                    if (parsed)
                    {
                        DateTimeOffset utcTime = DateTimeOffset.UnixEpoch.AddSeconds(timestampDouble);
                        tags["StartTime"] = utcTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff UTC");
                    }
                    else
                    {
                        throw new Exception("Could not parse start time: {startTimeTagValue}");
                    }
                }
                
                var endTimeTagValue = videoMetadata.EventEndTime;
                if (!string.IsNullOrEmpty(endTimeTagValue))
                {
                    var parsed = double.TryParse(endTimeTagValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var timestampDouble);
                    if (parsed)
                    {
                        DateTimeOffset utcTime = DateTimeOffset.UnixEpoch.AddSeconds(timestampDouble);
                        tags["EndTime"] = utcTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff UTC");
                    }
                    else
                    {
                        throw new Exception($"Could not parse start time: {endTimeTagValue}");
                    }
                }
                
                var zonesTagValue = videoMetadata.Zones;
                if (zonesTagValue != null && zonesTagValue.Length > 0)
                {
                    tags["Zones"] = string.Join(" ", zonesTagValue);
                }
                
                var blobClient = _blobContainerClient.GetBlobClient($"detections/{name}");
                await blobClient.SetMetadataAsync(tags);
                _logger.LogInformation("Tags are successfully set");

                List<PatchOperation> patchOperations = new List<PatchOperation>()
                {
                    PatchOperation.Replace("/isProcessed", true)
                };
                
                var patchResponse = await _cosmosContainer.PatchItemAsync<ObjectDetectionMetaDataDto>(cosmosId, cosmosPartitionKey, patchOperations);
                _logger.LogInformation("Metadata item patched: {status}", patchResponse.StatusCode);
            }
            else
            {
                throw new Exception($"Error checking existing record in CosmosDB for Event ID: {cosmosId}. Status Code: {response.StatusCode}");
            }
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "Error while processing name: {name}", name);
        }
    }
}
