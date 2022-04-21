using System.Text.Json.Serialization;

namespace Inedo.Extensions.Operations.Otter
{
    internal sealed class ScopedVariableJsonModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("sensitive")]
        public bool Sensitive { get; set; }

        [JsonPropertyName("server")]
        public string Server { get; set; }
        [JsonPropertyName("role")]
        public string ServerRole { get; set; }
        [JsonPropertyName("environment")]
        public string Environment { get; set; }
    }
}
