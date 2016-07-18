using System.ComponentModel;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
#endif
using Inedo.Serialization;

namespace Inedo.Extensions.Configurations.Files
{
    [DisplayName("Directory")]
    [DefaultProperty(nameof(Name))]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.Files.DirectoryConfiguration,OtterCoreEx")]
    public sealed class DirectoryConfiguration : PersistedConfiguration, IExistential
    {
        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Name")]
        [DisplayName("Name")]
        public string Name { get; set; }

        [Persistent]
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        [DisplayName("Exists")]
        public bool Exists { get; set; } = true;
    }
}
