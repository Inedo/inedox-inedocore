using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
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
using Inedo.Extensions.Credentials;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;
using LegacyUsernamePasswordCredentials = Inedo.Extensibility.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Operations.HTTP
{
    [Serializable]
    public abstract class HttpOperationBase : ExecuteOperation
    {
        protected HttpOperationBase()
        {
        }

        [Required]
        [ScriptAlias("Url")]
        [DisplayName("URL")]
        public string Url { get; set; }
        [Category("Options")]
        [ScriptAlias("LogResponseBody")]
        [DisplayName("Log response body")]
        public bool LogResponseBody { get; set; }
        [Category("Options")]
        [PlaceholderText("400:599")]
        [ScriptAlias("ErrorStatusCodes")]
        [DisplayName("Error status codes")]
        [Description("Comma-separated status codes (or ranges in the form of start:end) that should indicate this action has failed. "
                    + "For example, a value of \"401,500:599\" will fail on all server errors and also when \"HTTP Unauthorized\" is returned. "
                    + "The default is 400:599.")]
        public string ErrorStatusCodes { get; set; }
        [Category("Options")]
        [Output]
        [ScriptAlias("ResponseBody")]
        [DisplayName("Store response as")]
        [PlaceholderText("Do not store response body as variable")]
        public string ResponseBodyVariable { get; set; }
        [Category("Options")]
        [ScriptAlias("RequestHeaders")]
        [DisplayName("Request headers")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IDictionary<string, RuntimeValue> RequestHeaders { get; set; }
        [Category("Options")]
        [ScriptAlias("MaxResponseLength")]
        [DisplayName("Max response length")]
        [DefaultValue(1000)]
        public int MaxResponseLength { get; set; } = 1000;
        [Category("Options")]
        [ScriptAlias("ProxyRequest")]
        [DisplayName("Use server in context")]
        [Description("When selected, this will proxy the HTTP calls through the server is in context instead of using the server Otter or BuildMaster is installed on. If the server in context is SSH-based, then an error will be raised.")]
        [DefaultValue(true)]
        public bool ProxyRequest { get; set; } = true;

        [Category("Authentication")]
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        [SuggestableValue(typeof(SecureCredentialsSuggestionProvider<UsernamePasswordCredentials>))]
        public string CredentialName { get; set; }
        [Category("Authentication")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credential")]
        public string UserName { get; set; }
        [Category("Authentication")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from credential")]
        public string Password { get; set; }

        protected long TotalSize = 0;
        protected long CurrentPosition = 0;

        public override sealed Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrEmpty(this.CredentialName))
            {
                var cred = SecureCredentials.TryCreate(this.CredentialName, (ICredentialResolutionContext)context);
                var usernameCred = (cred ?? (cred as ResourceCredentials)?.ToSecureCredentials()) as UsernamePasswordCredentials;
                if (usernameCred == null)
                    this.LogWarning($"A username/password credential named \"{this.CredentialName}\" was not be found, and cannot be applied to the operation.");
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
            if (this.TotalSize == 0)
            {
                return null;
            }
            return new OperationProgress((int)(100 * this.CurrentPosition / this.TotalSize), AH.FormatSize(this.TotalSize - this.CurrentPosition) + " remaining");
        }

        protected HttpClient CreateClient()
        {
          
            HttpClient client;
            if (!string.IsNullOrWhiteSpace(this.UserName))
            {
                this.LogDebug($"Making request as {this.UserName}...");
                client = new HttpClient(new HttpClientHandler { Credentials = new NetworkCredential(this.UserName, this.Password ?? string.Empty) });
            }
            else
            {
                client = new HttpClient();
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

            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
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
        }

        private async Task<string> GetTruncatedResponseAsync(Stream stream)
        {
            using (var reader = new StreamReader(stream, InedoLib.UTF8Encoding))
            {
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

            var job = new RemoteHttpJob
            {
                Operation = this
            };
            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
            context.CancellationToken.Register(() => job.Cancel());
            var response = (HttpOperationBase)await executer.ExecuteJobAsync(job).ConfigureAwait(false);

            this.ResponseBodyVariable = response.ResponseBodyVariable;

        }

        protected abstract Task PerformRequestAsync(CancellationToken cancellationToken);

        private class RemoteHttpJob : RemoteJob
        {
            public HttpOperationBase Operation { get; set; }

            public override void Serialize(Stream stream)
            {
                new BinaryFormatter().Serialize(stream, this.Operation);
            }

            public override void Deserialize(Stream stream)
            {
                this.Operation = (HttpOperationBase)new BinaryFormatter().Deserialize(stream);
            }

            public override void SerializeResponse(Stream stream, object result)
            {
                new BinaryFormatter().Serialize(stream, this.Operation);
            }

            public override object DeserializeResponse(Stream stream)
            {
                return (HttpOperationBase)new BinaryFormatter().Deserialize(stream);
            }

            public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
            {
                this.Operation.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
                await this.Operation.PerformRequestAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }
        }
    }
}
