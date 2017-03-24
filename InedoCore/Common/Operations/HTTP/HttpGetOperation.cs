using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
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
    [DisplayName("HTTP GET Request")]
    [Description("Executes an HTTP GET, DELETE, or HEAD request against a URL, typically used for RESTful operations.")]
    [Tag("http")]
    [ScriptAlias("Get-Http")]
    [ScriptNamespace("HTTP", PreferUnqualified = true)]
    [DefaultProperty(nameof(Url))]
    [Example(@"
# downloads the http://httpbin.org/get page and stores its contents in the 
# $ResponseBody variable, failing only on 500 errors
Get-Http http://httpbin.org/get
(
    ErrorStatusCodes: 500:599,
    ResponseBody => $ResponseBody
);
")]
    [Serializable]
    public sealed class HttpGetOperation : HttpOperationBase
    {
        [ScriptAlias("Method")]
        [DefaultValue(GetHttpMethod.GET)]
        public GetHttpMethod Method { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            try
            {
                new Uri(this.Url);
            }
            catch (Exception ex)
            {
                this.LogError($"The {this.Method} request URL \"{this.Url}\" is invalid because: {ex.Message}");
                return;
            }

            this.LogInformation($"Performing HTTP {this.Method} request to {this.Url}...");

            await this.CallRemoteAsync(context).ConfigureAwait(false);
        }

        protected override async Task PerformRequestAsync(CancellationToken cancellationToken)
        {
            if (this.Method == GetHttpMethod.DELETE)
            {
                using (var client = this.CreateClient())
                using (var response = await client.DeleteAsync(this.Url, cancellationToken).ConfigureAwait(false))
                {
                    await this.ProcessResponseAsync(response).ConfigureAwait(false);
                }
            }
            else
            {
                using (var client = this.CreateClient())
                using (var response = await client.GetAsync(this.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    await this.ProcessResponseAsync(response).ConfigureAwait(false);
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "HTTP ",
                    AH.CoalesceString(config[nameof(this.Method)],
                    "GET")
                ),
                new RichDescription(
                    "from ",
                    new Hilite(config[nameof(this.Url)])
                )
            );
        }
    }

    public enum GetHttpMethod
    {
        GET,
        DELETE
    }
}
