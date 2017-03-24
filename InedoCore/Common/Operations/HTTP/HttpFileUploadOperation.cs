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

        public byte[] FileData { get; private set; }

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

            var fileName = context.ResolvePath(this.FileName);
            if (!await fileOps.FileExistsAsync(fileName).ConfigureAwait(false))
            {
                this.LogError($"The file \"{fileName}\" does not exist.");
                return;
            }

            this.LogInformation($"Uploading file {fileName} to {this.Url}...");
            this.FileData = await fileOps.ReadFileBytesAsync(fileName).ConfigureAwait(false);
            await this.CallRemoteAsync(context).ConfigureAwait(false);
            this.LogInformation("HTTP file upload completed.");
        }

        protected override async Task PerformRequestAsync(CancellationToken cancellationToken)
        {
            using (var client = this.CreateClient())
            using (var streamContent = new ByteArrayContent(this.FileData))
            using (var formData = new MultipartFormDataContent())
            {
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                formData.Add(streamContent, "file", PathEx.GetFileName(this.FileName));

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
