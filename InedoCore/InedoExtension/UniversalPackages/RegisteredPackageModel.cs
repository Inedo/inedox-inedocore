using System;
using Newtonsoft.Json;

namespace Inedo.Extensions.UniversalPackages
{
#warning rename to RemoteJson or something
    [JsonObject]
    internal sealed class RegisteredPackageModel
    {
        [JsonProperty("group", NullValueHandling = NullValueHandling.Ignore)]
        public string Group { get; set; }
        [JsonProperty("name")]
        public string Name { get; set;  }
        [JsonProperty("version")]
        public string Version { get; set; }
        [JsonProperty("path")]
        public string InstallPath { get; set; }
        [JsonProperty("feedUrl", NullValueHandling = NullValueHandling.Ignore)]
        public string FeedUrl { get; set; }
        [JsonProperty("installationDate", NullValueHandling = NullValueHandling.Ignore)]
        public string InstallationDate { get; set; }
        [JsonProperty("installationReason", NullValueHandling = NullValueHandling.Ignore)]
        public string InstallationReason { get; set; }
        [JsonProperty("installedUsing", NullValueHandling = NullValueHandling.Ignore)]
        public string InstalledUsing { get; set; }
        [JsonProperty("installedBy", NullValueHandling = NullValueHandling.Ignore)]
        public string InstalledBy { get; set; }

        public static bool NameAndGroupEquals(RegisteredPackageModel p1, RegisteredPackageModel p2)
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
