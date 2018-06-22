using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Web;

namespace Inedo.Extensions.Operations.HTTP
{
    [DisplayName("HTTP POST to URL")]
    [Description("Executes an HTTP POST/PUT/PATCH request to a URL, typically used for RESTful operations.")]
    [Tag("http")]
    [ScriptAlias("Post-Http")]
    [ScriptNamespace("HTTP", PreferUnqualified = true)]
    [DefaultProperty(nameof(Url))]
    [Example(@"
# posts some key-value pairs to a test service and writes the response body to the BuildMaster execution log
Post-Http http://httpbin.org/post
(
    FormData: %(
        Var1: ""value1"",
        Var2: ""value2""
    ),
    LogResponseBody: true
);
")]
    [Serializable]
    public sealed class HttpPostOperation : HttpOperationBase
    {
        [ScriptAlias("Method")]
        [DefaultValue(PostHttpMethod.POST)]
        public PostHttpMethod Method { get; set; } = PostHttpMethod.POST;
        private HttpMethod HttpMethod => new HttpMethod(this.Method.ToString());
        [Category("Data")]
        [ScriptAlias("ContentType")]
        [DisplayName("Content type")]
        [DefaultValue("application/x-www-form-urlencoded")]
        public string ContentType { get; set; }
        [Category("Data")]
        [ScriptAlias("TextData")]
        [DisplayName("Request text content")]
        [Description("Direct text input that will be written to the request content body. This will override any form data if both are supplied.")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string PostData { get; set; }
        [Category("Data")]
        [ScriptAlias("FormData")]
        [DisplayName("Form data")]
        [Description("A map of form data key/value pairs to send. If TextData is supplied, this value is ignored.")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IDictionary<string, RuntimeValue> FormData { get; set; }
        [Category("Options")]
        [ScriptAlias("LogRequestData")]
        [DisplayName("Log request data")]
        public bool LogRequestData { get; set; }

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

            if (this.LogRequestData)
            {
                using (var content = this.GetContent())
                {
                    this.LogDebug($"Request content: {await content.ReadAsStringAsync().ConfigureAwait(false)}");
                }
            }

            await this.CallRemoteAsync(context).ConfigureAwait(false);
        }

        protected override async Task PerformRequestAsync(CancellationToken cancellationToken)
        {
            using (var client = this.CreateClient())
            using (var content = this.GetContent())
            {
                using (var request = new HttpRequestMessage(this.HttpMethod, this.Url) { Content = content })
                using (var response = await client.SendAsync(request, cancellationToken))
                {
                    await this.ProcessResponseAsync(response).ConfigureAwait(false);
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP ", AH.CoalesceString(config[nameof(this.Method)], "POST")),
                new RichDescription("to ", new Hilite(config[nameof(this.Url)]))
            );
        }

        private HttpContent GetContent()
        {
            if (!string.IsNullOrEmpty(this.PostData))
                return new StringContent(this.PostData, InedoLib.UTF8Encoding, this.ContentType);

            if (this.FormData != null)
            {
                return new FormUrlEncodedContent(
                    from p in this.FormData
                    select new KeyValuePair<string, string>(p.Key, p.Value.AsString() ?? string.Empty)
                );
            }

            return new StringContent(string.Empty, InedoLib.UTF8Encoding, this.ContentType);
        }
    }

    public enum PostHttpMethod
    {
        POST,
        PUT,
        PATCH
    }
}
