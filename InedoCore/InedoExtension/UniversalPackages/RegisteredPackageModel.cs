using System;
using System.Text.Json.Serialization;

namespace Inedo.Extensions.UniversalPackages
{
    internal sealed class RegisteredPackageModel
    {
        [JsonPropertyName("group")]
        public string Group { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set;  }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("path")]
        public string InstallPath { get; set; }
        [JsonPropertyName("feedUrl")]
        public string FeedUrl { get; set; }
        [JsonPropertyName("installationDate")]
        public string InstallationDate { get; set; }
        [JsonPropertyName("installationReason")]
        public string InstallationReason { get; set; }
        [JsonPropertyName("installedUsing")]
        public string InstalledUsing { get; set; }
        [JsonPropertyName("installedBy")]
        public string InstalledBy { get; set; }

        public static bool NameAndGroupEquals(RegisteredPackageModel p1, RegisteredPackageModel p2)
        {
            if (ReferenceEquals(p1, p2))
                return true;
            if (p1 is null || p2 is null)
                return false;

            return string.Equals(p1.Group ?? string.Empty, p2.Group ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p1.Name, p2.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
