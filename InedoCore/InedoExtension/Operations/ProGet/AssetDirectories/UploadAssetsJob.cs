using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.IO;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    internal sealed class UploadAssetsJob : AssetDirectoryJob, IUploadAssets
    {
        public UploadAssetsJob()
        {
        }
        public UploadAssetsJob(UploadAssetsOperation operation) : base(operation)
        {
            this.Includes = operation.Includes?.ToList();
            this.Excludes = operation.Excludes?.ToList();
            this.SourceDirectory = operation.SourceDirectory;
        }

        public IReadOnlyList<string> Includes { get; set; }
        public IReadOnlyList<string> Excludes { get; set; }
        public string SourceDirectory { get; set; }

        IEnumerable<string> IUploadAssets.Includes => this.Includes;
        IEnumerable<string> IUploadAssets.Excludes => this.Excludes;

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            await this.UploadAssetsAsync(
                (p, m) => Task.FromResult(DirectoryEx.GetFileSystemInfos(p, m)),
                s => Task.FromResult<Stream>(FileEx.Open(s, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.Asynchronous | FileOptions.SequentialScan)),
                this.ReportProgress,
                cancellationToken
            );
            return null;
        }
        public override void Serialize(Stream stream)
        {
            base.Serialize(stream);
            using var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding, true);
            writer.Write(this.Includes?.Count ?? 0);
            foreach (var s in this.Includes ?? Enumerable.Empty<string>())
                writer.Write(s);
            writer.Write(this.Excludes?.Count ?? 0);
            foreach (var s in this.Excludes ?? Enumerable.Empty<string>())
                writer.Write(s);
            writer.Write(this.SourceDirectory ?? string.Empty);
        }
        public override void Deserialize(Stream stream)
        {
            base.Deserialize(stream);
            using var reader = new BinaryReader(stream, InedoLib.UTF8Encoding, true);
            int count = reader.ReadInt32();
            if (count > 0)
            {
                var includes = new string[count];
                for (int i = 0; i < includes.Length; i++)
                    includes[i] = reader.ReadString();
            }

            count = reader.ReadInt32();
            if (count > 0)
            {
                var excludes = new string[count];
                for (int i = 0; i < excludes.Length; i++)
                    excludes[i] = reader.ReadString();
            }

            this.SourceDirectory = reader.ReadString();
        }
    }
}
