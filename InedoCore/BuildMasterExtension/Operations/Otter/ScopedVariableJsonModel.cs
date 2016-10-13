using Newtonsoft.Json;

namespace Inedo.Extensions.Operations.Otter
{
    internal sealed class ScopedVariableJsonModel
    {
        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; set; }
        [JsonProperty("value", Required = Required.Always)]
        public string Value { get; set; }

        [JsonProperty("sensitive")]
        public bool Sensitive { get; set; }

        [JsonProperty("server")]
        public string Server { get; set; }
        [JsonProperty("role")]
        public string ServerRole { get; set; }
        [JsonProperty("environment")]
        public string Environment { get; set; }
    }
}
