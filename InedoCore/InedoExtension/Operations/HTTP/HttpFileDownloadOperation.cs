using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using Inedo.Serialization;

namespace Inedo.Extensions.Operations.HTTP
{
    [DisplayName("Download File from URL")]
    [Description("Downloads a file from a specified URL using an HTTP GET.")]
    [ScriptAlias("Download-Http")]
    [ScriptNamespace("HTTP", PreferUnqualified = true)]
    [DefaultProperty(nameof(Url))]
    [Example(@"
# downloads a file from example.org service endpoint
Download-Http http://example.org/upload-service/v3/hdars (
    To: ReleaseNotes.xml
);
")]
    [Serializable]
    public sealed class HttpFileDownloadOperation : HttpOperationBase
    {
        [Required]
        [SlimSerializable]
        [DisplayName("File name")]
        [ScriptAlias("FileName")]
        [Description("The destination path for the downloaded file.")]
        public string FileName { get; set; }

        [SlimSerializable]
        public string ResolvedFilePath { get; set; }

        protected override async Task ExecuteAsyncInternal(IOperationExecutionContext context)
        {
            try
            {
                new Uri(this.Url);
            }
            catch (Exception ex)
            {
                this.LogError($"The URL \"{this.Url}\" is invalid because: {ex.Message}");
                return;
            }

            this.ResolvedFilePath = context.ResolvePath(this.FileName);
            this.LogDebug("File path resolved to: " + this.ResolvedFilePath);
            if (!this.ProxyRequest)
            {
                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
                await fileOps.CreateDirectoryAsync(PathEx.GetDirectoryName(this.ResolvedFilePath)).ConfigureAwait(false);
                
                using var fileStream = await fileOps.OpenFileAsync(this.ResolvedFilePath, FileMode.Create, FileAccess.Write).ConfigureAwait(false);
                this.LogInformation($"Downloading {this.Url} to {this.FileName}...");
                await this.PerformRequestAsync(fileStream, context.CancellationToken).ConfigureAwait(false);
                this.LogInformation("HTTP file download completed.");
                return;
            }

            this.LogInformation($"Downloading {this.Url} to {this.FileName}...");
            await this.CallRemoteAsync(context).ConfigureAwait(false);
            this.LogInformation("HTTP file download completed.");
        }

        internal override async Task PerformRequestAsync(CancellationToken cancellationToken)
        {
            DirectoryEx.Create(PathEx.GetDirectoryName(this.ResolvedFilePath));

            using var fileStream = new FileStream(this.ResolvedFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await this.PerformRequestAsync(fileStream, cancellationToken);
        }

        private async Task PerformRequestAsync(Stream fileStream, CancellationToken cancellationToken)
        {
            using var client = this.CreateClient();
            using var response = await client.GetAsync(this.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var message = $"Server responded with status code {(int)response.StatusCode} - {response.ReasonPhrase}";
            if (!string.IsNullOrWhiteSpace(this.ErrorStatusCodes))
            {
                var errorCodeRanges = StatusCodeRangeList.Parse(this.ErrorStatusCodes);
                if (errorCodeRanges.IsInAnyRange((int)response.StatusCode))
                    this.LogError(message);
                else
                    this.LogInformation(message);
            }
            else
            {
                if (response.IsSuccessStatusCode)
                    this.LogInformation(message);
                else
                    this.LogError(message);
            }

            this.totalSize = response.Content.Headers.ContentLength ?? 0;

            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await responseStream.CopyToAsync(fileStream, 4096, cancellationToken, pos => this.currentPosition = pos).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP Download ", new Hilite(config[nameof(this.Url)])),
                new RichDescription("to ", new Hilite(config[nameof(this.FileName)]))
            );
        }
    }
}
