using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.AssetDirectories;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    [ScriptNamespace("ProGet")]
    [DefaultProperty(nameof(Path))]
    [ScriptAlias("Create-Directory")]
    [Description("Ensures that a subdirectory exists in a ProGet Asset Directory.")]
    [Example(@"
# ensures that the my/folder/path directory exists in the ProGet Asset Directory specified by the MyAssetDirResource secure resource
ProGet::Create-Directory my/folder/path
(
    Resource: MyAssetDirResource
);
")]
    public sealed class CreateSubdirectoryOperation : AssetDirectoryOperation
    {
        [Required]
        [ScriptAlias("Path")]
        public override string Path { get; set; }

        private protected override async Task ExecuteAsync(AssetDirectoryClient client, IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.Path))
            {
                this.LogWarning("Path is empty; nothing to do.");
                return;
            }

            this.LogInformation($"Creating {this.Path}...");
            await client.CreateDirectoryAsync(this.Path, context.CancellationToken);
            this.LogInformation($"{this.Path} created.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Create ",
                    new Hilite(this.Path),
                    " directory"
                ),
                new RichDescription(
                    "on ",
                    new Hilite(AH.CoalesceString(config[nameof(ApiUrl)], config[nameof(ResourceName)]))
                )
            );
        }
    }
}
