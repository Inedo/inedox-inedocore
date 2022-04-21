using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.Operations.Otter
{
    internal enum RemediationStatus { Pending, Completed, Faulted, Running, Disabled }

    internal interface IOtterClient
    {
        Task TriggerConfigurationCheckAsync(InfrastructureEntity entity);
        Task<ConfigurationStatusJsonModel> GetConfigurationStatusAsync(InfrastructureEntity entity);
        Task<string> TriggerRemediationJobAsync(InfrastructureEntity entity, string jobName = null);
        Task<RemediationStatus> GetRemediationJobStatusAsync(string jobToken);
        Task<IList<string>> EnumerateInfrastructureAsync(string entityType);
        Task SetGlobalVariableAsync(string name, string value);
        Task SetSingleVariableAsync(ScopedVariableJsonModel variable);
    }

    internal sealed class OtterClient : IOtterClient
    {
        private readonly string baseUrl;
        private readonly SecureString apiKey;
        private readonly ILogSink log;
        private readonly CancellationToken cancellationToken;

        public static IOtterClient Create(string server, SecureString apiKey, ILogSink log = null, CancellationToken? cancellationToken = null)
        {
            return new OtterClient(server, apiKey, log, cancellationToken);
        }

        private OtterClient(string server, SecureString apiKey, ILogSink log = null, CancellationToken? cancellationToken = null)
        {
            if (string.IsNullOrEmpty(server))
                throw new ArgumentNullException(nameof(server));

            this.baseUrl = server.TrimEnd('/');
            this.apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            this.log = log ?? (ILogSink)Logger.Null;
            this.cancellationToken = cancellationToken ?? CancellationToken.None;
        }

        public async Task TriggerConfigurationCheckAsync(InfrastructureEntity entity)
        {
            using var client = this.CreateClient();
            string url = $"api/configuration/check?{entity.Type}={Uri.EscapeDataString(entity.Name)}";
            this.LogRequest(url);
            using var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);
        }

        public async Task<ConfigurationStatusJsonModel> GetConfigurationStatusAsync(InfrastructureEntity entity)
        {
            using var client = this.CreateClient();
            string url = $"api/configuration/status?{entity.Type}={Uri.EscapeDataString(entity.Name)}";
            this.LogRequest(url);

            using var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<ConfigurationStatusJsonModel>(stream);
        }

        public async Task<string> TriggerRemediationJobAsync(InfrastructureEntity entity, string jobName = null)
        {
            using var client = this.CreateClient();
            string url = $"api/configuration/remediate/{entity.Type}/{Uri.EscapeDataString(entity.Name)}";
            if (jobName != null)
                url += $"?job={jobName}";

            using var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public async Task<RemediationStatus> GetRemediationJobStatusAsync(string jobToken)
        {
            using var client = this.CreateClient();
            string url = $"api/configuration/remediate/status?token={Uri.EscapeDataString(jobToken)}";
            this.LogRequest(url);
            using var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);

            string status = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (Enum.TryParse(status, ignoreCase: true, result: out RemediationStatus result))
                return result;
            else
                throw new OtterException(500, "Unexpected remediation job status returned from Otter: " + status);
        }

        public async Task<IList<string>> EnumerateInfrastructureAsync(string entityType)
        {
            using var client = this.CreateClient();
            string url = $"api/infrastructure/{entityType}s/list";
            this.LogRequest(url);
            var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var jsonReader = JsonDocument.Parse(stream);
            if (jsonReader.RootElement.ValueKind != JsonValueKind.Array)
                throw new OtterException(400, $"Expected StartArray token from Otter Infrastructure API, was '{jsonReader.RootElement.ValueKind}'.");

            var entities = new List<string>();

            foreach (var element in jsonReader.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                    entities.Add(nameElement.GetString());
            }

            return entities;
        }

        public async Task SetGlobalVariableAsync(string name, string value)
        {
            using var client = this.CreateClient();
            string url = $"api/variables/global/{Uri.EscapeDataString(name)}";

            this.LogRequest(url);

            using var content = new StringContent(value);
            using var response = await client.PostAsync(url, content).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);
        }

        public async Task SetSingleVariableAsync(ScopedVariableJsonModel variable)
        {
            if (string.IsNullOrEmpty(variable.Server) && string.IsNullOrEmpty(variable.ServerRole) && string.IsNullOrEmpty(variable.Environment))
                throw new InvalidOperationException("Specified variable requires at least one scope (server, role, or environment).");

            using var client = this.CreateClient();
            string url = "api/variables/scoped/single";
            this.LogRequest(url);
            using var stream = new TemporaryStream();
            JsonSerializer.Serialize(stream, variable);
            stream.Position = 0;

            using var content = new StreamContent(stream);
            using var response = await client.PostAsync(url, content).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);
        }

        private void LogRequest(string relativeUrl)
        {
            string url = this.baseUrl + '/' + relativeUrl;
            this.log.LogDebug("Creating request to URL: " + url);
        }

        private HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(this.baseUrl, UriKind.Absolute)
            };

            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(typeof(Operation).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product, typeof(Operation).Assembly.GetName().Version.ToString()));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InedoCore", typeof(OtterClient).Assembly.GetName().Version.ToString()));
            client.DefaultRequestHeaders.Add("X-ApiKey", AH.Unprotect(this.apiKey));
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

    internal abstract class InfrastructureEntity
    {
        public static readonly string Server = "server";
        public static readonly string Role = "role";
        public static readonly string Environment = "environment";

        public string Name { get; }
        public abstract string Type { get; }

        protected InfrastructureEntity(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public static InfrastructureEntity Create(string serverName = null, string roleName = null)
        {
            if (!string.IsNullOrEmpty(serverName))
                return new ServerEntity(serverName);
            else if (!string.IsNullOrEmpty(roleName))
                return new RoleEntity(roleName);
            else
                return null;
        }
        
        public override string ToString() => $"{this.Type} '{this.Name}'";

        private sealed class ServerEntity : InfrastructureEntity
        {
            public override string Type => Server;
            public ServerEntity(string name)
                : base(name)
            {
            }
        }
        private sealed class RoleEntity : InfrastructureEntity
        {
            public override string Type => InfrastructureEntity.Role;
            public RoleEntity(string name)
                : base(name)
            {
            }
        }
        private sealed class EnvironmentEntity : InfrastructureEntity
        {
            public override string Type => InfrastructureEntity.Environment;
            public EnvironmentEntity(string name)
                : base(name)
            {
            }
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
