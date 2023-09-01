using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
    [Description("Uploads a file to a specified URL using an HTTP POST or PUT.")]
    [ScriptAlias("Upload-Http")]
    [ScriptNamespace("HTTP", PreferUnqualified = true)]
    [DefaultProperty(nameof(FileName))]
    [Example(@"
# uploads a file to example.org service endpoint
Upload-Http ReleaseNotes.xml (
    To: http://example.org/upload-service/v3/hdars
);
")]
    [Serializable]
    public sealed class HttpFileUploadOperation : HttpOperationBase
    {
        [SlimSerializable]
        [ScriptAlias("Method")]
        [DefaultValue(PostHttpMethod.POST)]
        public PostHttpMethod Method { get; set; }
        private HttpMethod HttpMethod => new(this.Method.ToString());
        [Required]
        [SlimSerializable]
        [DisplayName("File name")]
        [ScriptAlias("FileName")]
        [Description("The path of the file to upload.")]
        public string FileName { get; set; }

        [SlimSerializable]
        public string ResolvedFilePath { get; set; }

        protected override async Task ExecuteAsyncInternal(IOperationExecutionContext context)
        {
            try
            {
                _ = new Uri(this.Url);
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
                using var fileStream = await fileOps.OpenFileAsync(this.ResolvedFilePath, FileMode.Open, FileAccess.Read).ConfigureAwait(false);
                this.LogInformation($"Uploading file {this.FileName} to {this.Url}...");
                await this.PerformRequestAsync(fileStream, context.CancellationToken).ConfigureAwait(false);
                this.LogInformation("HTTP file upload completed.");
                return;
            }

            this.LogInformation($"Uploading file {this.FileName} to {this.Url}...");
            await this.CallRemoteAsync(context).ConfigureAwait(false);
            this.LogInformation("HTTP file upload completed.");
        }

        internal override async Task PerformRequestAsync(CancellationToken cancellationToken)
        {
            if (!FileEx.Exists(this.ResolvedFilePath))
            {
                this.LogDebug($"The file \"{this.ResolvedFilePath}\" does not exist.");
                return;
            }

            using var fileStream = new FileStream(this.ResolvedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await this.PerformRequestAsync(fileStream, cancellationToken);
        }

        private async Task PerformRequestAsync(Stream fileStream, CancellationToken cancellationToken)
        {
            using var client = this.CreateClient();
            using var streamContent = new StreamContent(fileStream);
            using var formData = new MultipartFormDataContent();
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            formData.Add(streamContent, "file", PathEx.GetFileName(this.ResolvedFilePath));

            using var request = new HttpRequestMessage(this.HttpMethod, this.Url) { Content = formData };
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await this.ProcessResponseAsync(response).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP Upload ", new Hilite(config[nameof(this.FileName)])),
                new RichDescription("to ", new Hilite(config[nameof(this.Url)]))
            );
        }
    }
}
