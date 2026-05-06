using CameraEvent.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CameraEvent.Functions;

public class CreateObejctDetectionMetaData
{
    private readonly ILogger<CreateObejctDetectionMetaData> _logger;
    private readonly Container _cosmosContainer;

    public CreateObejctDetectionMetaData(IConfiguration config, CosmosClient cosmosClient, ILogger<CreateObejctDetectionMetaData> logger)
    {
        _logger = logger;
        _cosmosContainer = cosmosClient.GetContainer(config["CosmosDatabaseName"], config["CosmosContainerName"]);
    }

    [Function("CreateObejctDetectionMetaData")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        _logger.LogInformation("CreateObejctDetectionMetaData function started.");

        try
        {
        if (_cosmosContainer == null)
        {
            throw new Exception("CosmosDB container is not initialized. Check configuration.");
        }

        var inputMetaData = await req.ReadFromJsonAsync<ObjectDetectionMetaDataDto>();
        if (inputMetaData == null || string.IsNullOrWhiteSpace(inputMetaData.Id) || string.IsNullOrWhiteSpace(inputMetaData.CameraId))
        {
            _logger.LogWarning("Invalid input data in function app.");
            return new BadRequestObjectResult("Invalid input data in function app.");
        }

        using var response = await _cosmosContainer.ReadItemStreamAsync(inputMetaData.Id, new PartitionKey(inputMetaData.CameraId));

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Creating new record in CosmosDB {record}", inputMetaData);
            await _cosmosContainer.CreateItemAsync(inputMetaData, new PartitionKey(inputMetaData.CameraId));
            return new OkObjectResult(inputMetaData);
        }
        else if (response.IsSuccessStatusCode)
        {
            var existingRecord = await JsonSerializer.DeserializeAsync<ObjectDetectionMetaDataDto>(response.Content);
            if (existingRecord.UpdateFields(inputMetaData))
            {
                _logger.LogInformation("Update existing record {record} with {newrecord}", existingRecord, inputMetaData);
                await _cosmosContainer.ReplaceItemAsync(existingRecord, existingRecord.Id, new PartitionKey(existingRecord.CameraId));
            }
            else
            {
                _logger.LogInformation("No changes detected for record {record} with incoming data {newrecord}", existingRecord, inputMetaData);
            }
            return new OkObjectResult(existingRecord);
        }
        else
        {
            throw new Exception($"Error checking existing record in CosmosDB for Event ID: {inputMetaData.Id}. Status Code: {response.StatusCode}");
        }
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "Error while processing metadata");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
   }
}