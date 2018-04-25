using System.ComponentModel;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.Extensions.Configurations.Network
{
    [DisplayName("Hosts File Entry")]
    [DefaultProperty(nameof(HostName))]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.Network.HostsEntryConfiguration,OtterCoreEx")]
    public sealed class HostsEntryConfiguration : PersistedConfiguration, IExistential
    {
        [Required]
        [ConfigurationKey]
        [Persistent]
        [ScriptAlias("Host")]
        [DisplayName("Host name")]
        public string HostName { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("IP")]
        [DisplayName("IP address")]
        public string IpAddress { get; set; }

        [Persistent]
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        public bool Exists { get; set; } = true;
    }
}
