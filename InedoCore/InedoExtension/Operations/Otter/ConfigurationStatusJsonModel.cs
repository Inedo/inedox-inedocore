using System;
using System.Text.Json.Serialization;

namespace Inedo.Extensions.Operations.Otter
{
    internal sealed class ConfigurationStatusJsonModel
    {
        public ConfigurationStatusJsonModel()
        {
        }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("errorText")]
        public string ErrorText { get; set; }

        [JsonPropertyName("collectionDate")]
        public DateTime? CollectionDate { get; set; }

        [JsonPropertyName("latestCollectionId")]
        public int? LatestCollectionId { get; set; }

        [JsonPropertyName("remediationDate")]
        public DateTime? RemediationDate { get; set; }

        [JsonPropertyName("remediationId")]
        public int? RemediationId { get; set; }
    }
}
