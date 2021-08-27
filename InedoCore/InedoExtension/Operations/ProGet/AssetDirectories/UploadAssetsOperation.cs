using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.AssetDirectories;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    [Tag("assets")]
    [ScriptNamespace("ProGet")]
    [ScriptAlias("Upload-Assets")]
    [DisplayName("Upload Assets to ProGet")]
    [Description("Uploads files to a ProGet Asset Directory.")]
    [Example(@"
# upload all .zip files from the working directory to the remotedir/subdir directory on the MyAssetDirResource asset directory
ProGet::Upload-Assets
(
    From: $WorkingDirectory,
    To: remotedir/subdir,
    Include: *.zip,
    Resource: MyAssetDirResource
);
")]
    public sealed class UploadAssetsOperation : AssetDirectoryOperation, IUploadAssets
    {
        [ScriptAlias("To")]
        [DisplayName("To")]
        public override string Path { get; set; }
        [ScriptAlias("Include")]
        [MaskingDescription]
        [PlaceholderText("* (top-level items)")]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("From")]
        [PlaceholderText("$WorkingDirectory")]
        [DisplayName("Source directory")]
        public string SourceDirectory { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ProxyData")]
        [DisplayName("Proxy data through BuildMaster/Otter")]
        [Description("When true, requests will be made from the BuildMaster/Otter server instead of directly from the server in context.")]
        public bool ProxyRequest { get; set; }

        private protected override async Task ExecuteAsync(AssetDirectoryClient client, IOperationExecutionContext context)
        {
            this.SourceDirectory = context.ResolvePath(this.SourceDirectory);
            if (this.ProxyRequest)
            {
                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
                await this.UploadAssetsAsync(fileOps.GetFileSystemInfosAsync, n => fileOps.OpenFileAsync(n, FileMode.Open, FileAccess.Read), this.ProgressReceived, context.CancellationToken);
            }
            else
            {
                var job = new UploadAssetsJob(this) { ProgressReceived = this.ProgressReceived };
                job.MessageLogged += (s, e) => this.Log(e);
                var jobRunner = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();
                await jobRunner.ExecuteJobAsync(job, context.CancellationToken);
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Upload ",
                    new MaskHilite(config[nameof(Includes)], config[nameof(Excludes)])
                ),
                new RichDescription(
                    "to ",
                    new Hilite(AH.CoalesceString(config[nameof(Path)], "/")),
                    " on ",
                    new Hilite(AH.CoalesceString(config[nameof(ApiUrl)], config[nameof(ResourceName)]))
                )
            );
        }
    }
}
