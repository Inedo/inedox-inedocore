using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Diagnostics;
using Newtonsoft.Json;

namespace Inedo.Extensions.Operations.Otter
{
    internal enum EntityType { Server, Role }

    internal enum RemediationStatus { Pending, Completed, Faulted, Running, Disabled }

    internal sealed class OtterClient
    {
        private string baseUrl;
        private SecureString apiKey;
        private ILogger log;
        private CancellationToken cancellationToken;

        public OtterClient(string server, SecureString apiKey, ILogger log, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(server))
                throw new ArgumentNullException(nameof(server));
            if (apiKey == null)
                throw new ArgumentNullException(nameof(apiKey));

            this.baseUrl = server.TrimEnd('/');
            this.apiKey = apiKey;
            this.log = log ?? new ProGet.NullLogger();
            this.cancellationToken = cancellationToken;
        }

        public async Task TriggerConfigurationCheckAsync(EntityType entityType, string entityName)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/configuration/check?{entityType.ToString().ToLowerInvariant()}={Uri.EscapeDataString(entityName)}";
                this.LogRequest(url);
                await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<ConfigurationStatusJsonModel> GetConfigurationStatusAsync(EntityType entityType, string entityName)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/configuration/status?{entityType.ToString().ToLowerInvariant()}={Uri.EscapeDataString(entityName)}";
                this.LogRequest(url);

                var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);

                await HandleError(response).ConfigureAwait(false);

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    var result = JsonSerializer.CreateDefault().Deserialize<ConfigurationStatusJsonModel>(jsonReader);
                    return result;
                }
            }
        }

        public async Task<string> TriggerRemediationJobAsync(EntityType entityType, string entityName, string jobName = null)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/configuration/remediate/{entityType.ToString().ToLowerInvariant()}/{Uri.EscapeDataString(entityName)}";
                if (jobName != null)
                    url += $"?job={jobName}";

                var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
                await HandleError(response).ConfigureAwait(false);

                string jobToken = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return jobToken;
            }
        }

        public async Task<RemediationStatus> GetRemediationJobStatusAsync(string jobToken)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/configuration/remediate/status?token={Uri.EscapeDataString(jobToken)}";
                this.LogRequest(url);
                var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
                await HandleError(response).ConfigureAwait(false);

                string status = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                RemediationStatus result;
                if (Enum.TryParse(status, ignoreCase: true, result: out result))
                    return result;
                else
                    throw new OtterException(500, "Unexpected remediation job status returned from Otter: " + status);
            }
        }

        private void LogRequest(string relativeUrl)
        {
            string url = this.baseUrl + '/' + relativeUrl;
            this.log.LogDebug("Creating request to URL: " + url);
        }

        private HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(this.baseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(typeof(Operation).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product, typeof(Operation).Assembly.GetName().Version.ToString()));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InedoCore", typeof(OtterClient).Assembly.GetName().Version.ToString()));
            client.DefaultRequestHeaders.Add("X-ApiKey", this.apiKey.ToUnsecureString());

            return client;
        }

        private static async Task HandleError(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.InternalServerError && message.StartsWith("<!DOCTYPE"))
                message = "Invalid Otter API URL. Ensure the URL follows the format: http://{otter-server}";

            throw new OtterException((int)response.StatusCode, message);
        }
    }

    internal sealed class OtterException : Exception
    {
        public OtterException(int statusCode, string message)
            : base(message)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; set; }

        public string FullMessage => $"The server returned an error ({this.StatusCode}): {this.Message}";
    }
}
