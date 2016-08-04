using System;
using System.ComponentModel;
using System.Net;
using Inedo.Documentation;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.HTTP
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.HTTP.HttpGetAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.HTTP.Get))]
    [DisplayName("HTTP GET/DELETE/HEAD Request")]
    [Description("Executes an HTTP GET/DELETE/HEAD request against a URL, typically used for RESTful operations.")]
    [CustomEditor(typeof(HttpGetActionEditor))]
    [Tag(Tags.Http)]
    public sealed class HttpGetAction : HttpActionBase
    {
        public HttpGetAction()
        {
            this.HttpMethod = "GET";
        }

        [Persistent]
        public string Url { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP ", this.HttpMethod),
                new RichDescription("from ", new Hilite(this.Url))
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
            this.PerformRequest(request);
            return null;
        }
    }
}
