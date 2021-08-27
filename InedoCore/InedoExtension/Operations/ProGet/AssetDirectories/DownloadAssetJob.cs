using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Inedo.IO;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    internal sealed class DownloadAssetJob : AssetDirectoryJob, IDownloadAsset
    {
        public DownloadAssetJob()
        {
        }
        public DownloadAssetJob(DownloadAssetOperation operation) : base(operation)
        {
            this.TargetDirectory = operation.TargetDirectory;
        }

        public string TargetDirectory { get; set; }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            await this.DownloadAssetAsync(
                p =>
                {
                    DirectoryEx.Create(p);
                    return InedoLib.CompletedTask;
                },
                s => Task.FromResult<Stream>(FileEx.Open(s, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.Asynchronous | FileOptions.SequentialScan)),
                this.ReportProgress,
                cancellationToken
            );
            return null;
        }
        public override void Serialize(Stream stream)
        {
            base.Serialize(stream);
            using var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding, true);
            writer.Write(this.TargetDirectory ?? string.Empty);
        }
        public override void Deserialize(Stream stream)
        {
            base.Deserialize(stream);
            using var reader = new BinaryReader(stream, InedoLib.UTF8Encoding, true);
            this.TargetDirectory = reader.ReadString();
        }
    }
}
