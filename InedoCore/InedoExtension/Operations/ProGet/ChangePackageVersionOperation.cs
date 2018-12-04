using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using Inedo.UPack;
using Inedo.UPack.Packaging;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [DisplayName("Change Package Version")]
    [Description("Changes the version number of a universal package and adds a repackaging entry to its metadata.")]
    [ScriptNamespace(Namespaces.ProGet)]
    [ScriptAlias("Repack-Package")]
    public sealed class ChangePackageVersionOperation : RemoteExecuteOperation
    {
        [Required]
        [ScriptAlias("FileName")]
        [DisplayName("Package file name")]
        public string FileName { get; set; }
        [ScriptAlias("NewVersion")]
        [DisplayName("New version")]
        [PlaceholderText("Remove pre-release and build metadata labels")]
        public string NewVersion { get; set; }
        [ScriptAlias("Reason")]
        [DisplayName("Reason")]
        [PlaceholderText("Unspecified")]
        public string Reason { get; set; }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var fullPath = context.ResolvePath(this.FileName);

            this.LogInformation($"Changing \"{fullPath}\" package version to {AH.CoalesceString(this.NewVersion, "remove pre-release label")}...");

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
            try
            {
                DirectoryEx.Create(tempPath);
                UniversalPackageMetadata currentMetadata;
                using (var upack = new UniversalPackage(fullPath))
                {
                    currentMetadata = upack.GetFullMetadata();
                    await upack.ExtractAllItemsAsync(tempPath, context.CancellationToken);
                    FileEx.Delete(PathEx.Combine(tempPath, "upack.json"));
                }

                var newMetadata = currentMetadata.Clone();
                if (string.IsNullOrEmpty(this.NewVersion))
                    newMetadata.Version = new UniversalPackageVersion(currentMetadata.Version.Major, currentMetadata.Version.Minor, currentMetadata.Version.Patch);
                else
                    newMetadata.Version = UniversalPackageVersion.Parse(this.NewVersion);

                if (currentMetadata.Version == newMetadata.Version)
                {
                    this.LogWarning($"Current package version {currentMetadata.Version} and the new version {newMetadata.Version} are the same; nothing to do.");
                    return null;
                }

                this.LogInformation("New version: " + newMetadata.Version);

                this.LogDebug("Adding repacking entry...");
                newMetadata.RepackageHistory.Add(
                    new RepackageHistoryEntry
                    {
                        Id = new UniversalPackageId(currentMetadata.Group, currentMetadata.Name) + ":" + currentMetadata.Version,
                        Date = DateTimeOffset.Now,
                        Using = SDK.ProductName + "/" + SDK.ProductVersion,
                        Reason = this.Reason
                    }
                );

                using (var builder = new UniversalPackageBuilder(fullPath, newMetadata))
                {
                    await builder.AddRawContentsAsync(tempPath, string.Empty, true, c => true, context.CancellationToken);
                }

                this.LogInformation("Package version changed.");

                return null;
            }
            finally
            {
                try
                {
                    this.LogDebug($"Deleting temporary files from {tempPath}...");
                    DirectoryEx.Clear(tempPath);
                    DirectoryEx.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    this.LogWarning("Unable to delete temporary files: " + ex.Message);
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Change ",
                    new DirectoryHilite(config[nameof(FileName)]),
                    " package version to ",
                    new Hilite(AH.CoalesceString(config[nameof(NewVersion)], "remove pre-release"))
                )
            );
        }
    }
}
