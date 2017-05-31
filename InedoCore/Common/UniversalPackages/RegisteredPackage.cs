using Newtonsoft.Json;

namespace Inedo.Extensions.UniversalPackages
{
    [JsonObject]
    internal sealed class RegisteredPackage
    {
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
    }
}
