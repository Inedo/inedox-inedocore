using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
using System.Threading;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Configurations;
using Inedo.Hedgehog.Extensibility.Credentials;
using Inedo.Hedgehog.Extensibility.Operations;
using Inedo.Hedgehog.Extensibility.RaftRepositories;
using Inedo.Hedgehog.Web;
using Inedo.Hedgehog.Web.Controls;
using Inedo.Hedgehog.Web.Controls.Plans;
#endif

namespace Inedo.Extensions.Operations.HTTP
{
    [DisplayName("Upload File to URL")]
    [Description("Uploads a file to a specified URL using an HTTP POST.")]
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
        [Required]
        [DisplayName("File name")]
        [ScriptAlias("FileName")]
        [Description("The path of the file to upload.")]
        public string FileName { get; set; }

        public string ResolvedFilePath { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
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

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            this.ResolvedFilePath = context.ResolvePath(this.FileName);
            this.LogDebug("File path resolved to: " + this.ResolvedFilePath);

            this.LogInformation($"Uploading file {this.FileName} to {this.Url}...");
            await this.CallRemoteAsync(context).ConfigureAwait(false);
            this.LogInformation("HTTP file upload completed.");
        }

        protected override async Task PerformRequestAsync(CancellationToken cancellationToken)
        {
            if (!FileEx.Exists(this.ResolvedFilePath))
            {
                this.LogDebug($"The file \"{this.ResolvedFilePath}\" does not exist.");
                return;
            }

            using (var client = this.CreateClient())
            using (var fileStream = new FileStream(this.ResolvedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var streamContent = new StreamContent(fileStream))
            using (var formData = new MultipartFormDataContent())
            {
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                formData.Add(streamContent, "file", PathEx.GetFileName(this.ResolvedFilePath));

                using (var response = await client.PostAsync(this.Url, formData, cancellationToken).ConfigureAwait(false))
                {
                    await this.ProcessResponseAsync(response).ConfigureAwait(false);
                }
            }
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
