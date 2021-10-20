using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.AssetDirectories;
using Inedo.Diagnostics;
using Inedo.IO;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    internal static class RemotableOperations
    {
        public static async Task UploadAssetsAsync(this IUploadAssets input, Func<string, MaskingContext, Task<IList<SlimFileSystemInfo>>> getFileSystemInfosAsync, Func<string, Task<Stream>> openSourceAsync, Action<int?, string> reportProgress, CancellationToken cancellationToken)
        {
            var matches = (await getFileSystemInfosAsync(input.SourceDirectory, new MaskingContext(input.Includes, input.Excludes)))
                .OfType<SlimFileInfo>()
                .ToList();

            if (matches.Count == 0)
            {
                input.LogWarning("No files matched the specified file mask.");
                return;
            }

            long totalSize = matches.Sum(f => f.Size);
            long copiedBytes = 0;

            var client = CreateClient(input);

            foreach (var m in matches)
            {
                input.LogInformation($"Uploading {m.FullName} ({AH.FormatSize(m.Size)})...");
                var targetPath = ResolveTargetPath(input.SourceDirectory, m.FullName, input.Path ?? string.Empty);
                using var source = await openSourceAsync(m.FullName);
                using var target = await client.UploadMultipartFileAsync(targetPath, m.Size, cancellationToken: cancellationToken);
                await CopyToAsync(source, target, copiedBytes, totalSize, m.Name, reportProgress, cancellationToken);
                input.LogInformation($"{targetPath} created.");
            }
        }
        public static async Task DownloadAssetAsync(this IDownloadAsset input, Func<string, Task> createDirectoryAsync, Func<string, Task<Stream>> openTargetAsync, Action<int?, string> reportProgress, CancellationToken cancellationToken)
        {
            var client = CreateClient(input);

            var info = await client.TryGetItemMetadataAsync(input.Path, cancellationToken);
            if (info == null)
            {
                input.LogError($"{input.Path} does not exist.");
                return;
            }

            using var source = await client.DownloadFileAsync(input.Path, cancellationToken);

            await createDirectoryAsync(input.TargetDirectory);
            var targetFileName = PathEx.Combine(input.TargetDirectory, PathEx.GetFileName(input.Path));
            using var target = await openTargetAsync(targetFileName);

            await CopyToAsync(source, target, 0, info.Length.GetValueOrDefault(), info.Name, reportProgress, cancellationToken);
        }

        private static AssetDirectoryClient CreateClient(IRemotableAssetOperation input) => new(input.ApiUrl, AH.NullIf(input.ApiKey, string.Empty), AH.NullIf(input.UserName, string.Empty), AH.NullIf(input.Password, string.Empty));
        private static string ResolveTargetPath(string sourceRootPath, string sourcePath, string targetPath) => PathEx.MakeCanonical(PathEx.Combine('/', targetPath, sourcePath.Substring(sourceRootPath.Length).Trim('/', '\\')));

        private static async Task CopyToAsync(Stream source, Stream target, long copiedAtStart, long length, string messagePrefix, Action<int?, string> reportProgress, CancellationToken cancellationToken)
        {
            // use this to keep from spamming the connection with status updates
            var lastReportTime = DateTime.MinValue;

            await source.CopyToAsync(target, 81920, cancellationToken, report);

            void report(long copied)
            {
                var now = DateTime.UtcNow;
                if (now.Subtract(lastReportTime) >= new TimeSpan(0, 0, 1))
                {
                    int percent = (int)(100 * (copied + copiedAtStart) / length);
                    var message = $"{messagePrefix} ({AH.FormatSize(length - copied - copiedAtStart)} remaining)";
                    reportProgress(percent, message);
                    lastReportTime = now;
                }
            }
        }
    }
}
