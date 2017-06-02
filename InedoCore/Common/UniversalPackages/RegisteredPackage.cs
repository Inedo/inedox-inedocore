using System;
using Newtonsoft.Json;

namespace Inedo.Extensions.UniversalPackages
{
    [JsonObject]
    internal sealed class RegisteredPackage
    {
        [JsonProperty("group")]
        public string Group { get; set; }
        [JsonProperty("name")]
        public string Name { get; set;  }
        [JsonProperty("version")]
        public string Version { get; set; }
        [JsonProperty("installPath")]
        public string InstallPath { get; set; }
        [JsonProperty("feedUrl")]
        public string FeedUrl { get; set; }
        [JsonProperty("installationDate")]
        public string InstallationDate { get; set; }
        [JsonProperty("installationReason")]
        public string InstallationReason { get; set; }
        [JsonProperty("installedUsing")]
        public string InstalledUsing { get; set; }
        [JsonProperty("installedBy")]
        public string InstalledBy { get; set; }

        public static bool NameAndGroupEquals(RegisteredPackage p1, RegisteredPackage p2)
        {
            if (ReferenceEquals(p1, p2))
                return true;
            if (ReferenceEquals(p1, null) | ReferenceEquals(p2, null))
                return false;

            return string.Equals(p1.Group ?? string.Empty, p2.Group ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p1.Name, p2.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
