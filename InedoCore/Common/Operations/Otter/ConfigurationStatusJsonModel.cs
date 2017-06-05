using System;
using Newtonsoft.Json;

namespace Inedo.Extensions.Operations.Otter
{
    internal sealed class ConfigurationStatusJsonModel
    {
        public ConfigurationStatusJsonModel()
        {
        }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("errorText", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorText { get; set; }

        [JsonProperty("collectionDate")]
        public DateTime? CollectionDate { get; set; }

        [JsonProperty("latestCollectionId")]
        public int? LatestCollectionId { get; set; }

        [JsonProperty("remediationDate", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? RemediationDate { get; set; }

        [JsonProperty("remediationId", NullValueHandling = NullValueHandling.Ignore)]
        public int? RemediationId { get; set; }
    }
}
