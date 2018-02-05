using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using Inedo.Documentation;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.HTTP
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.HTTP.HttpPostAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.HTTP.Post))]
    [DisplayName("HTTP POST/PUT/PATCH to URL")]
    [Description("Executes an HTTP POST/PUT/PATCH request to a URL, typically used for RESTful operations.")]
    [Inedo.Web.CustomEditor(typeof(HttpPostActionEditor))]
    [Tag(Tags.Http)]
    public sealed class HttpPostAction : HttpActionBase
    {
        public HttpPostAction()
        {
            this.HttpMethod = "POST";
        }

        [Persistent]
        public string Url { get; set; }
        [Persistent]
        public string PostData { get; set; }
        [Persistent]
        public string ContentType { get; set; }
        [Persistent(CustomSerializer = typeof(FormDataConverter))]
        [CustomVariableReplacer(typeof(FormDataConverter))]
        public IList<KeyValuePair<string, string>> FormData { get; set; }
        [Persistent]
        public bool LogRequestData { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP ", this.HttpMethod),
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
                this.LogError("The {0} request URL \"{1}\" is invalid because: {2}", this.HttpMethod, this.Url, ex.Message);
                return;
            }

            this.LogInformation("Performing HTTP {0} request to the URL \"{1}\"...", this.HttpMethod, this.Url);
            this.ExecuteRemoteCommand(null);

            this.SetResponseBodyVariable();

            this.LogInformation("HTTP {0} request completed.", this.HttpMethod);
        }
        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            var request = (HttpWebRequest)WebRequest.Create(this.Url);
            request.Method = this.HttpMethod;

            request.ContentType = AH.CoalesceStr(this.ContentType, "application/x-www-form-urlencoded");

            this.LogDebug("Request Content-Type: " + request.ContentType);

            if (this.LogRequestData)
            {
                var buffer = new StringBuilder();
                buffer.Append("Request content: ");

                if (!string.IsNullOrEmpty(this.PostData))
                {
                    buffer.Append(this.PostData);
                }
                else if (this.FormData != null && this.FormData.Count > 0)
                {
                    bool first = true;
                    foreach (var field in this.FormData)
                    {
                        if (!first)
                            buffer.Append('&');
                        else
                            first = false;

                        buffer.Append(Uri.EscapeDataString(field.Key));
                        buffer.Append('=');
                        buffer.Append(Uri.EscapeDataString(field.Value));
                    }
                }

                this.LogDebug(buffer.ToString());
            }

            if (!string.IsNullOrEmpty(this.PostData))
            {
                using (var requestStream = request.GetRequestStream())
                using (var sw = new StreamWriter(requestStream, new UTF8Encoding(false)))
                {
                    sw.Write(this.PostData);
                }
            }
            else if (this.FormData != null && this.FormData.Count > 0)
            {
                using (var requestStream = request.GetRequestStream())
                using (var sw = new StreamWriter(requestStream, new UTF8Encoding(false)))
                {
                    bool first = true;

                    foreach (var field in this.FormData)
                    {
                        if (!first)
                            sw.Write('&');
                        else
                            first = false;

                        sw.Write(Uri.EscapeDataString(field.Key));
                        sw.Write('=');
                        sw.Write(Uri.EscapeDataString(field.Value));
                    }
                }
            }

            this.PerformRequest(request);

            return null;
        }
    }
}
