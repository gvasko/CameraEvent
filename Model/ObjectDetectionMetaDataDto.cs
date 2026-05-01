using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CameraEvent.Model;

public class ObjectDetectionMetaDataDto
{
    /// <summary>
    /// Event ID
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("cameraId")]
    [JsonProperty("cameraId")]
    public string CameraId { get; set; }

    [JsonPropertyName("eventStartTime")]
    [JsonProperty("eventStartTime")]
    public string EventStartTime { get; set; }
    
    [JsonPropertyName("eventEndTime")]
    [JsonProperty("eventEndTime")]
    public string EventEndTime { get; set; }
    
    [JsonPropertyName("objectType")]
    [JsonProperty("objectType")]
    public string ObjectType { get; set; }

    [JsonPropertyName("zones")]
    [JsonProperty("zones")]
    public string[] Zones { get; set; } = [];

    [JsonPropertyName("isProcessed")]
    [JsonProperty("isProcessed")]
    public bool IsProcessed { get; set; } = false;


    public override string ToString()
        => $"Id={Id}, CameraId={CameraId}, EventStartTime={EventStartTime}, EventEndTime={EventEndTime}, ObjectType={ObjectType}, Zones=[{string.Join(",", Zones)}]";

    internal bool UpdateFields(ObjectDetectionMetaDataDto inputMetaData)
    {
        var changed = false;
        
        if (!string.IsNullOrWhiteSpace(inputMetaData.EventStartTime) && inputMetaData.EventStartTime != EventStartTime)
        {
            EventStartTime = inputMetaData.EventStartTime;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(inputMetaData.EventEndTime) && inputMetaData.EventEndTime != EventEndTime)
        {
            EventEndTime = inputMetaData.EventEndTime;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(inputMetaData.ObjectType) && inputMetaData.ObjectType != ObjectType)
        {
            ObjectType = inputMetaData.ObjectType;
            changed = true;
        }
        if (inputMetaData.Zones != null && inputMetaData.Zones.Length > 0 && !inputMetaData.Zones.All(z => Zones.Contains(z)))
        {
            Zones = Zones.Union(inputMetaData.Zones).ToArray();
            changed = true;
        }
        return changed;
    }
}
