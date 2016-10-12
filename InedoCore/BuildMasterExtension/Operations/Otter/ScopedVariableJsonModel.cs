using System;
using Newtonsoft.Json;

namespace Inedo.Extensions.Operations.Otter
{
    internal sealed class ScopedVariableJsonModel : IEquatable<ScopedVariableJsonModel>
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

        public static bool Equals(ScopedVariableJsonModel var1, ScopedVariableJsonModel var2)
        {
            if (ReferenceEquals(var1, var2))
                return true;
            if (ReferenceEquals(var1, null) | ReferenceEquals(var2, null))
                return false;

            if (!string.Equals(var1.Name, var2.Name, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(var1.Server, var2.Server, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(var1.ServerRole, var2.ServerRole, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(var1.Environment, var2.Environment, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        public bool Equals(ScopedVariableJsonModel other) => Equals(this, other);
        public override bool Equals(object obj) => this.Equals(obj as ScopedVariableJsonModel);
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name ?? string.Empty);
    }
}
