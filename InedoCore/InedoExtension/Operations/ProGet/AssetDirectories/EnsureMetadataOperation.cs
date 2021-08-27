using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.AssetDirectories;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    [Tag("assets")]
    [ScriptNamespace("ProGet")]
    [ScriptAlias("Ensure-Metadata")]
    [DisplayName("Ensure Asset Directory Item Metadata")]
    [Description("Ensures that metadata exists on an Asset Directory item.")]
    [Example(@"
# ensures that the my/folder/path directory exists in the ProGet Asset Directory specified by the MyAssetDirResource secure resource
ProGet::Ensure-Metadata
(
    Path: assetitem.html,
    Metadata: %(CreatedFor: $ApplicationName, Release: $ReleaseNumber),
    Resource: MyAssetDirResource
);
")]
    public sealed class EnsureMetadataOperation : AssetDirectoryOperation
    {
        [Required]
        [ScriptAlias("Path")]
        public override string Path { get; set; }
        [ScriptAlias("Metadata")]
        public IDictionary<string, RuntimeValue> Metadata { get; set; }

        private protected override async Task ExecuteAsync(AssetDirectoryClient client, IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.Path))
            {
                this.LogError("Path is empty.");
                return;
            }

            if (this.Metadata == null || this.Metadata.Count == 0)
            {
                this.LogWarning("No metadata was specified.");
                return;
            }

            this.LogDebug($"Metadata values for {this.Path}:");
            var metadata = new Dictionary<string, UserMetadataValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in this.Metadata)
            {
                this.LogDebug($"{m.Key} = {m.Value}");
                metadata[m.Key] = m.Value.AsString() ?? string.Empty;
            }

            this.LogInformation($"Updating metadata for {this.Path}...");
            await client.UpdateItemMetadataAsync(this.Path, userMetadata: metadata, cancellationToken: context.CancellationToken);
            this.LogInformation("Metadata updated");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure metadata for ",
                    new Hilite(config[nameof(Path)])
                ),
                new RichDescription(
                    "on ",
                    new Hilite(AH.CoalesceString(config[nameof(ApiUrl)], config[nameof(ResourceName)]))
                )
            );
        }
    }
}
