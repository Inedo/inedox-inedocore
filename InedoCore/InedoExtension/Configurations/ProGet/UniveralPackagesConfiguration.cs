using System;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Serialization;

namespace Inedo.Extensions.Configurations.ProGet
{
    /// <summary>
    /// Provides additional metadata for installed universal packages.
    /// </summary>
    [Serializable]
    [SlimSerializable]
    [ScriptAlias("UPack")]
    public sealed class UniveralPackagesConfiguration : PackageConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UniveralPackagesConfiguration"/> class.
        /// </summary>
        public UniveralPackagesConfiguration()
        {
        }

        /// <summary>
        /// Gets or sets the package installation path.
        /// </summary>
        [Persistent]
        public string Path { get; set; }
        /// <summary>
        /// Gets or sets a valud indicating whether the package is cached.
        /// </summary>
        [Persistent]
        public bool Cached { get; set; }
        /// <summary>
        /// Gets or sets the package installation date.
        /// </summary>
        [Persistent]
        public string Date { get; set; }
        /// <summary>
        /// Gets or sets the package installation reason.
        /// </summary>
        [Persistent]
        public string Reason { get; set; }
        /// <summary>
        /// Gets or sets the name of the tool used to install the package.
        /// </summary>
        [Persistent]
        public string Tool { get; set; }
        /// <summary>
        /// Gets or sets the name of the user that installed the package.
        /// </summary>
        [Persistent]
        public string User { get; set; }
    }
}
