using System;
using System.ComponentModel;
using System.IO;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
#elif Hedgehog
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Configurations;
#endif
using Inedo.Serialization;

namespace Inedo.Extensions.Configurations.Files
{
    [DisplayName("File")]
    [DefaultProperty(nameof(Name))]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.Files.FileConfiguration,OtterCoreEx")]
    public sealed class FileConfiguration : PersistedConfiguration, IExistential
    {
        [Persistent]
        public byte[] Contents { get; set; }

        [Persistent]
        [ScriptAlias("Text")]
        [DisplayName("Text contents")]
        [Description("The contents of the file. A missing or empty value indicates the file should be a 0-byte file.")]
        public string TextContents { get; set; }

        [Persistent]
        [ScriptAlias("ReadOnly")]
        [DisplayName("Read Only")]
        [Description("Indicates that the file should be marked with the read-only attribute. Note that when this value is set, it is "
            + "applied after the FileAttributes value, which will override the readonly flag specified in that property.")]
        public bool? IsReadOnly { get; set; }

        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Name")]
        [DisplayName("Name")]
        [Description("The name or path of the file or directory.")]
        public string Name { get; set; }

        [Persistent]
        [ScriptAlias("Attributes")]
        [DisplayName("Attributes")]
        [Description("The attributes for the file or directory. These may be entered as an integer flag or by name. Common values are ReadOnly=1, Hidden=2, System=4, Archive=32, and Normal=128. "
            + "Integral values may be ORed together to specify any combination of attributes, except for \"Normal (128)\", which may only be used alone.")]
        public FileAttributes? Attributes { get; set; }

        [Persistent]
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        [DisplayName("Exists")]
        public bool Exists { get; set; } = true;

        [Persistent]
        [ScriptAlias("Modified")]
        [DisplayName("Last write time")]
        [Description("The last write time (UTC) of the file or directory.")]
        public DateTime? LastWriteTimeUtc { get; set; }
    }
}