using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.AssetDirectories;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    [ScriptNamespace("ProGet")]
    [DefaultProperty(nameof(Path))]
    [ScriptAlias("Download-Asset")]
    [Description("Downloads a file from a ProGet Asset Directory.")]
    [Example(@"
# download dir/info.txt to the current working directory from the MyAssetDirResource asset directory
ProGet::Download-Asset
(
    From: dir/info.txt,
    To: $WorkingDirectory,
    Resource: MyAssetDirResource
);
")]
    public sealed class DownloadAssetOperation : AssetDirectoryOperation, IDownloadAsset
    {
        [Required]
        [ScriptAlias("From")]
        [DisplayName("From")]
        public override string Path { get; set; }
        [ScriptAlias("To")]
        [DisplayName("To")]
        public string TargetDirectory { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ProxyData")]
        [DisplayName("Proxy data through BuildMaster/Otter")]
        [Description("When true, requests will be made from the BuildMaster/Otter server instead of directly from the server in context.")]
        public bool ProxyRequest { get; set; }

        private protected override async Task ExecuteAsync(AssetDirectoryClient client, IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.Path))
            {
                this.LogError("Missing \"From\" value.");
                return;
            }

            this.TargetDirectory = context.ResolvePath(this.TargetDirectory);

            if (this.ProxyRequest)
            {
                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
                await this.DownloadAssetAsync(fileOps.CreateDirectoryAsync, p => fileOps.OpenFileAsync(p, FileMode.Create, FileAccess.Write), this.ProgressReceived, context.CancellationToken);
            }
            else
            {
                var job = new DownloadAssetJob(this) { ProgressReceived = this.ProgressReceived };
                job.MessageLogged += (s, e) => this.Log(e);
                var jobRunner = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();
                await jobRunner.ExecuteJobAsync(job, context.CancellationToken);
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Download ",
                    new Hilite(config[nameof(Path)])
                ),
                new RichDescription(
                    "from ",
                    new Hilite(AH.CoalesceString(config[nameof(ApiUrl)], config[nameof(ResourceName)])),
                    " to ",
                    new DirectoryHilite(config[nameof(TargetDirectory)])
                )
            );
        }
    }
}
