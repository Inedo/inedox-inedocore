using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Serialization;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Operations.HTTP
{
    [Serializable]
    [SlimSerializable]
    public abstract class HttpOperationBase : ExecuteOperation
    {
        protected long totalSize;
        protected long currentPosition;

        protected HttpOperationBase()
        {
        }

        [Required]
        [ScriptAlias("Url")]
        [DisplayName("URL")]
        [SlimSerializable]
        public string Url { get; set; }
        [SlimSerializable]
        [Category("Options")]
        [ScriptAlias("LogResponseBody")]
        [DisplayName("Log response body")]
        public bool LogResponseBody { get; set; }
        [SlimSerializable]
        [Category("Options")]
        [PlaceholderText("400:599")]
        [ScriptAlias("ErrorStatusCodes")]
        [DisplayName("Error status codes")]
        [Description("Comma-separated status codes (or ranges in the form of start:end) that should indicate this action has failed. "
                    + "For example, a value of \"401,500:599\" will fail on all server errors and also when \"HTTP Unauthorized\" is returned. "
                    + "The default is 400:599.")]
        public string ErrorStatusCodes { get; set; }
        [Output]
        [Category("Options")]
        [ScriptAlias("ResponseBody")]
        [DisplayName("Store response as")]
        [PlaceholderText("Do not store response body as variable")]
        public string ResponseBodyVariable { get; set; }
        [SlimSerializable]
        [Category("Options")]
        [ScriptAlias("RequestHeaders")]
        [DisplayName("Request headers")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IDictionary<string, RuntimeValue> RequestHeaders { get; set; }
        [SlimSerializable]
        [Category("Options")]
        [ScriptAlias("MaxResponseLength")]
        [DisplayName("Max response length")]
        [DefaultValue(1000)]
        public int MaxResponseLength { get; set; } = 1000;
        [SlimSerializable]
        [Category("Options")]
        [ScriptAlias("ProxyRequest")]
        [DisplayName("Use server in context")]
        [Description("When selected, this will proxy the HTTP calls through the server is in context instead of using the server Otter or BuildMaster is installed on.")]
        [DefaultValue(true)]
        public bool ProxyRequest { get; set; } = true;

        [Category("Authentication")]
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        [SuggestableValue(typeof(SecureCredentialsSuggestionProvider<UsernamePasswordCredentials>))]
        public string CredentialName { get; set; }
        [SlimSerializable]
        [Category("Authentication")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credential")]
        public string UserName { get; set; }
        [SlimSerializable]
        [Category("Authentication")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from credential")]
        public string Password { get; set; }
        [SlimSerializable]
        [Category("Options")]
        [ScriptAlias("IgnoreSslErrors")]
        [DisplayName("Ignore SSL Errors")]
        [DefaultValue(false)]
        public bool IgnoreSslErrors { get; set; }

        public override sealed Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrEmpty(this.CredentialName))
            {
                var cred = SecureCredentials.TryCreate(this.CredentialName, (ICredentialResolutionContext)context);
                if ((cred ?? (cred as ResourceCredentials)?.ToSecureCredentials()) is not UsernamePasswordCredentials usernameCred)
                {
                    this.LogWarning($"A username/password credential named \"{this.CredentialName}\" was not be found, and cannot be applied to the operation.");
                }
                else
                {
                    this.LogDebug($"Applying \"{this.CredentialName}\" credential (UserName=\"{usernameCred.UserName}\") to the operation...");
                    this.UserName = usernameCred.UserName;
                    this.Password = AH.Unprotect(usernameCred.Password);
                }
            }

            return this.ExecuteAsyncInternal(context);
        }
        protected abstract Task ExecuteAsyncInternal(IOperationExecutionContext context);

        public override OperationProgress GetProgress()
        {
            if (this.totalSize == 0)
                return null;

            return new OperationProgress((int)(100 * this.currentPosition / this.totalSize), AH.FormatSize(this.totalSize - this.currentPosition) + " remaining");
        }

        protected HttpClient CreateClient()
        {
            HttpClient client;
            if (!string.IsNullOrWhiteSpace(this.UserName))
            {
                this.LogDebug($"Making request as {this.UserName}...");
                var handler = new HttpClientHandler { Credentials = new NetworkCredential(this.UserName, this.Password ?? string.Empty) };
                if (this.IgnoreSslErrors)
                    IgnoreSslErrorsOnHttpClientHandler(handler);

                client = new HttpClient(handler);
            }
            else
            {
                var handler = new HttpClientHandler();
                if (this.IgnoreSslErrors)
                    IgnoreSslErrorsOnHttpClientHandler(handler);

                client = new HttpClient(handler);
            }

            client.Timeout = Timeout.InfiniteTimeSpan;

            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(typeof(Operation).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product, typeof(Operation).Assembly.GetName().Version.ToString()));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InedoCore", typeof(HttpOperationBase).Assembly.GetName().Version.ToString()));

            if (this.RequestHeaders != null)
            {
                foreach (var header in this.RequestHeaders)
                {
                    this.LogDebug($"Adding request header {header.Key}={header.Value.AsString()}...");
                    client.DefaultRequestHeaders.Add(header.Key, header.Value.AsString() ?? string.Empty);
                }
            }

            return client;
        }

        private void IgnoreSslErrorsOnHttpClientHandler(HttpClientHandler handler)
        {
            // This can be replaced with the following when we upgrade .NET Framework to 4.7.1+ or to .NET Core
            // handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true;
            var handlerType = handler.GetType();
            var property = handlerType.GetProperty("ServerCertificateCustomValidationCallback");
            if (property != null)
                property.SetValue(handler, (Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool>)((message, cert, chain, sslPolicy) => true));
            else
                this.LogWarning("Ignore SSL Errors is enabled but HttpClientHandler is missing the ServerCertificateCustomValidationCallback.  Cannot bypass SSL errors.");
        }

        protected async Task ProcessResponseAsync(HttpResponseMessage response)
        {
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

            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var text = await this.GetTruncatedResponseAsync(responseStream).ConfigureAwait(false);
            if (string.IsNullOrEmpty(text))
            {
                this.LogDebug("The Content Length of the response was 0.");
            }
            else
            {
                this.ResponseBodyVariable = text;
                if (this.LogResponseBody)
                {
                    if (text.Length >= this.MaxResponseLength)
                        this.LogDebug($"The following response Content Body is truncated to {this.MaxResponseLength} characters.");

                    this.LogInformation("Response Content Body: " + text);
                }
            }
        }

        private async Task<string> GetTruncatedResponseAsync(Stream stream)
        {
            using var reader = new StreamReader(stream, InedoLib.UTF8Encoding);
            var text = new StringBuilder();
            var buffer = new char[1024];
            int remaining = this.MaxResponseLength;

            int read = await reader.ReadAsync(buffer, 0, Math.Min(remaining, 1024)).ConfigureAwait(false);
            while (read > 0 && remaining > 0)
            {
                text.Append(buffer, 0, read);
                remaining -= read;
                if (remaining > 0)
                    read = await reader.ReadAsync(buffer, 0, Math.Min(remaining, 1024)).ConfigureAwait(false);
            }

            return text.ToString();
        }

        protected async Task CallRemoteAsync(IOperationExecutionContext context)
        {
            if (!this.ProxyRequest)
            {
                await this.PerformRequestAsync(context.CancellationToken).ConfigureAwait(false);
                return;
            }

            var executer = await context.Agent.TryGetServiceAsync<IRemoteJobExecuter>().ConfigureAwait(false);
            if (executer == null)
            {
                this.LogError($"\"Use server in context\" is not supported for this agent: {context.Agent.GetDescription()}");
                return;
            }

            var job = new RemoteHttpJob { Operation = this };
            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
            context.CancellationToken.Register(() => job.Cancel());
            var response = (string)await executer.ExecuteJobAsync(job).ConfigureAwait(false);

            this.ResponseBodyVariable = response;
        }

        internal abstract Task PerformRequestAsync(CancellationToken cancellationToken);
    }
}
