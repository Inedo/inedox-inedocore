using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.HTTP
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.HTTP.HttpFileUploadAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.HTTP.Upload))]
    [DisplayName("Upload File to URL")]
    [Description("Uploads a file to a specified URL using an HTTP POST.")]
    [CustomEditor(typeof(HttpFileUploadActionEditor))]
    [Tag(Tags.Http)]
    [Tag(Tags.Files)]
    public sealed class HttpFileUploadAction : HttpActionBase
    {
        [Persistent]
        public string FileName { get; set; }
        [Persistent]
        public string Url { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP Upload ", new Hilite(this.FileName)),
                new RichDescription("to ", new Hilite(this.Url))
            );
        }

        protected override void Execute()
        {
            try
            {
                new Uri(this.Url);
            }
            catch (Exception ex)
            {
                this.LogError("The URL \"{0}\" is invalid because: {1}", this.Url, ex.Message);
                return;
            }

            this.LogInformation("Uploading file from \"{0}\" to \"{1}\"...", this.FileName, this.Url);
            if (this.Context.Agent.TryGetService<IRemoteJobExecuter>() != null)
                this.ExecuteRemoteCommand(null);
            else
                this.UploadFile();
            this.LogInformation("HTTP file upload completed.");
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            this.UploadFile();
            return string.Empty;
        }

        private void UploadFile()
        {
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();

            var boundary = "-------------------------" + DateTime.UtcNow.Ticks.ToString("x");

            var request = (HttpWebRequest)WebRequest.Create(this.Url);
            request.Method = "POST";
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            using (var requestStream = request.GetRequestStream())
            {
                var filePath = fileOps.CombinePath(this.Context.SourceDirectory, this.FileName);
                this.LogDebug("Uploading file {0}...", filePath);
                if (!fileOps.FileExists(filePath))
                {
                    this.LogError("The file \"{0}\" does not exist.", filePath);
                    return;
                }

                var requestWriter = new StreamWriter(requestStream, new UTF8Encoding(false));
                requestWriter.WriteLine("--" + boundary);
                requestWriter.WriteLine("Content-Disposition: form-data;name=\"file\";filename=\"{0}\"", PathEx.GetFileName(filePath));
                requestWriter.WriteLine("Content-Type: application/octet-stream");
                requestWriter.WriteLine();
                requestWriter.Flush();

                using (var sourceStream = fileOps.OpenFile(filePath, FileMode.Open, FileAccess.Read))
                {
                    sourceStream.CopyTo(requestStream);
                }

                requestWriter.WriteLine("\r\n--" + boundary + "--");
                requestWriter.Flush();
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                {
                    var buffer = new byte[16384];
                    int length = responseStream.Read(buffer, 0, buffer.Length);

                    if (length == 0)
                    {
                        this.LogDebug("Response body is empty.");
                    }
                    else
                    {
                        try
                        {
                            this.LogInformation(Encoding.UTF8.GetString(buffer, 0, length));
                        }
                        catch
                        {
                            this.LogInformation("The response could not be parsed as a string; responded with {0} bytes of binary data.", length);
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                this.ProcessResponse((HttpWebResponse)ex.Response);
            }
        }
    }
}
