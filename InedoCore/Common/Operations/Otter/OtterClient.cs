using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.IO;
using Newtonsoft.Json;

#if BuildMaster
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility.Operations;
#elif Hedgehog
using Inedo.Extensibility.Operations;
#endif

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

#if Otter
    internal sealed class SelfOtterClient : IOtterClient
    {
        private ILogger log;
        private CancellationToken cancellationToken;

        internal SelfOtterClient(ILogger log = null, CancellationToken? cancellationToken = null)
        {
            this.log = log;
            this.cancellationToken = cancellationToken ?? CancellationToken.None;
        }

        public async Task TriggerConfigurationCheckAsync(InfrastructureEntity entity)
        {
            using (var db = new DB.Context())
            {
                int? serverId = null;
                int? serverRoleId = null;

                if (string.Equals(entity.Type, InfrastructureEntity.Server, StringComparison.OrdinalIgnoreCase))
                {
                    serverId = (await db.Servers_GetServerByNameAsync(entity.Name).ConfigureAwait(false)).Servers_Extended.FirstOrDefault()?.Server_Id;
                    if (serverId == null)
                        throw new OtterException(400, $"Server '{entity.Name}' not found.");
                }
                else if (string.Equals(entity.Type, InfrastructureEntity.Role, StringComparison.OrdinalIgnoreCase))
                {
                    serverRoleId = (await db.ServerRoles_GetServerRolesAsync().ConfigureAwait(false)).FirstOrDefault(r => string.Equals(r.ServerRole_Name, entity.Name, StringComparison.OrdinalIgnoreCase))?.ServerRole_Id;
                    if (serverRoleId == null)
                        throw new OtterException(400, $"Server role '{entity.Name}' not found.");
                }

                // TODO: Plugins cannot currently trigger configuration checks
                throw new OtterException(403, "An API key is required to check configuration for a server.");
            }
        }

        public async Task<ConfigurationStatusJsonModel> GetConfigurationStatusAsync(InfrastructureEntity entity)
        {
            using (var db = new DB.Context())
            {
                if (string.Equals(entity.Type, InfrastructureEntity.Server, StringComparison.OrdinalIgnoreCase))
                {
                    var server = (await db.Servers_GetServerByNameAsync(entity.Name).ConfigureAwait(false)).Servers_Extended.FirstOrDefault();
                    if (server == null)
                        throw new OtterException(404, $"Server {entity.Name} not found.");

                    return new ConfigurationStatusJsonModel(server);
                }
                else if (string.Equals(entity.Type, InfrastructureEntity.Role, StringComparison.OrdinalIgnoreCase))
                {
                    var role = (await db.ServerRoles_GetServerRolesAsync().ConfigureAwait(false)).FirstOrDefault(r => string.Equals(r.ServerRole_Name, entity.Name, StringComparison.OrdinalIgnoreCase));
                    if (role == null)
                        throw new OtterException(404, $"Server role {entity.Name} not found.");

                    return new ConfigurationStatusJsonModel(role);
                }
                else if (string.Equals(entity.Type, InfrastructureEntity.Environment, StringComparison.OrdinalIgnoreCase))
                {
                    var env = (await db.Environments_GetEnvironmentsAsync().ConfigureAwait(false)).FirstOrDefault(e => string.Equals(e.Environment_Name, entity.Name, StringComparison.OrdinalIgnoreCase));
                    if (env == null)
                        throw new OtterException(404, $"Environment {entity.Name} not found.");

                    return new ConfigurationStatusJsonModel(env);
                }
                else
                {
                    throw new OtterException(400, $"Entity type '{entity.Type}' is invalid, expected server, role, or environment.");
                }
            }
        }

        public async Task<string> TriggerRemediationJobAsync(InfrastructureEntity entity, string jobName = null)
        {
            using (var db = new DB.Context())
            {
                int? serverId = null;
                int? serverRoleId = null;

                if (string.Equals(entity.Type, InfrastructureEntity.Server, StringComparison.OrdinalIgnoreCase))
                {
                    serverId = (await db.Servers_GetServerByNameAsync(entity.Name).ConfigureAwait(false)).Servers_Extended.FirstOrDefault()?.Server_Id;
                    if (serverId == null)
                        throw new OtterException(400, $"Server '{entity.Name}' not found.");
                }
                else if (string.Equals(entity.Type, InfrastructureEntity.Role, StringComparison.OrdinalIgnoreCase))
                {
                    serverRoleId = (await db.ServerRoles_GetServerRolesAsync().ConfigureAwait(false)).FirstOrDefault(r => string.Equals(r.ServerRole_Name, entity.Name, StringComparison.OrdinalIgnoreCase))?.ServerRole_Id;
                    if (serverRoleId == null)
                        throw new OtterException(400, $"Server role '{entity.Name}' not found.");
                }
                else
                {
                    throw new OtterException(400, $"Entity type '{entity.Type ?? "(null)"}' is invalid, expected server or role.");
                }

                return (await db.Jobs_CreateJobAsync(
                    JobType_Code: Domains.JobTypeCode.Configuration,
                    Plan_Name: null,
                    Raft_Id: null,
                    Schedule_Id: null,
                    Job_Name: jobName,
                    JobTarget_Code: serverId != null ? Domains.JobTargetCode.Direct : Domains.JobTargetCode.Indirect,
                    Start_Date: DateTime.UtcNow,
                    Simulation_Indicator: false,
                    ServerIds_Csv: serverId?.ToString(),
                    ServerRoleIds_Csv: serverRoleId?.ToString(),
                    EnvironmentIds_Csv: null
                ).ConfigureAwait(false)).Value.ToString();
            }
        }

        public async Task<RemediationStatus> GetRemediationJobStatusAsync(string jobToken)
        {
            int? id = AH.ParseInt(jobToken);
            if (id == null)
                throw new OtterException(400, "A token argument is required.");

            var job = (await new DB.Context(false).Jobs_GetJobAsync(id).ConfigureAwait(false)).Jobs_Extended.FirstOrDefault();

            if (job == null)
                throw new OtterException(400, "Invalid job token.");

            var status = Domains.JobStateCode.GetName(job.JobState_Code);

            RemediationStatus result;
            if (Enum.TryParse(status, ignoreCase: true, result: out result))
                return result;
            else
                throw new OtterException(500, "Unexpected remediation job status returned from Otter: " + status);
        }

        public async Task<IList<string>> EnumerateInfrastructureAsync(string entityType)
        {
            using (var db = new DB.Context())
            {
                var infrastructure = await db.Infrastructure_GetInfrastructureAsync().ConfigureAwait(false);

                if (string.Equals(entityType, InfrastructureEntity.Server, StringComparison.OrdinalIgnoreCase))
                {
                    return infrastructure.Servers_Extended.Select(s => s.Server_Name).ToList();
                }
                else if (string.Equals(entityType, InfrastructureEntity.Role, StringComparison.OrdinalIgnoreCase))
                {
                    return infrastructure.ServerRoles_Extended.Select(r => r.ServerRole_Name).ToList();
                }
                else if (string.Equals(entityType, InfrastructureEntity.Environment, StringComparison.OrdinalIgnoreCase))
                {
                    return infrastructure.Environments_Extended.Select(e => e.Environment_Name).ToList();
                }
                else
                {
                    throw new OtterException(400, "Invalid \"entry-type\" in URL.");
                }
            }
        }

        public async Task SetGlobalVariableAsync(string name, string value)
        {
            await new DB.Context(false).Variables_CreateOrUpdateVariableAsync(
                Variable_Name: name,
                ValueType_Code: value.StartsWith("@(") ? Domains.VariableValueType.Scalar : value.StartsWith("%(") ? Domains.VariableValueType.Map : Domains.VariableValueType.Scalar,
                Variable_Value: InedoLib.UTF8Encoding.GetBytes(value),
                Sensitive_Indicator: false,
                EvaluateVariables_Indicator: false
            ).ConfigureAwait(false);
        }

        public async Task SetSingleVariableAsync(ScopedVariableJsonModel variable)
        {
            using (var db = new DB.Context())
            {
                db.BeginTransaction();

                int? serverId = null;
                int? serverRoleId = null;
                int? environmentId = null;

                if (!string.IsNullOrEmpty(variable.Server))
                {
                    serverId = (await db.Servers_GetServerByNameAsync(variable.Server).ConfigureAwait(false)).Servers_Extended.FirstOrDefault()?.Server_Id;
                    if (serverId == null)
                        throw new OtterException(400, $"Invalid server \"{variable.Server}\" on {variable.Name} variable.");
                }

                if (!string.IsNullOrEmpty(variable.ServerRole))
                {
                    serverRoleId = (await db.ServerRoles_GetServerRolesAsync().ConfigureAwait(false)).FirstOrDefault(r => string.Equals(r.ServerRole_Name, variable.ServerRole, StringComparison.OrdinalIgnoreCase))?.ServerRole_Id;
                    if (serverRoleId == null)
                        throw new OtterException(400, $"Invalid role \"{variable.ServerRole}\" on {variable.Name} variable.");
                }

                if (!string.IsNullOrEmpty(variable.Environment))
                {
                    environmentId = (await db.Environments_GetEnvironmentsAsync().ConfigureAwait(false)).FirstOrDefault(e => string.Equals(e.Environment_Name, variable.Environment, StringComparison.OrdinalIgnoreCase))?.Environment_Id;
                    if (environmentId == null)
                        throw new OtterException(400, $"Invalid environment \"{variable.Environment}\" on {variable.Name} variable.");
                }

                if ((serverId ?? serverRoleId ?? environmentId) == null)
                    throw new OtterException(400, $"Variable {variable.Name} is missing server, role, and environment properties.");

                await db.Variables_CreateOrUpdateVariableAsync(
                    Variable_Name: variable.Name,
                    ValueType_Code: variable.Value.StartsWith("@(") ? Domains.VariableValueType.Scalar : variable.Value.StartsWith("%(") ? Domains.VariableValueType.Map : Domains.VariableValueType.Scalar,
                    Variable_Value: InedoLib.UTF8Encoding.GetBytes(variable.Value),
                    Sensitive_Indicator: variable.Sensitive,
                    EvaluateVariables_Indicator: false,
                    Server_Id: serverId,
                    ServerRole_Id: serverRoleId,
                    Environment_Id: environmentId
                ).ConfigureAwait(false);

                db.CommitTransaction();
            }
        }
    }
#endif

    internal sealed class OtterClient : IOtterClient
    {
        private string baseUrl;
        private SecureString apiKey;
        private ILogger log;
        private CancellationToken cancellationToken;

#if Hedgehog
        public static IOtterClient Create(string server, SecureString apiKey, ILogSink log, CancellationToken? cancellationToken = null)
            => Create(server, apiKey, new ShimLogger(log), cancellationToken);
#endif

        public static IOtterClient Create(string server, SecureString apiKey, ILogger log = null, CancellationToken? cancellationToken = null)
        {
#if Otter
            if (string.IsNullOrEmpty(server) && (apiKey == null || apiKey.Length == 0))
            {
                return new SelfOtterClient(log, cancellationToken);
            }
#endif

            return new OtterClient(server, apiKey, log, cancellationToken);
        }

        private OtterClient(string server, SecureString apiKey, ILogger log = null, CancellationToken? cancellationToken = null)
        {
            if (string.IsNullOrEmpty(server))
                throw new ArgumentNullException(nameof(server));

            this.baseUrl = server.TrimEnd('/');
            this.apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            this.log = log ?? Logger.Null;
            this.cancellationToken = cancellationToken ?? CancellationToken.None;
        }

        public async Task TriggerConfigurationCheckAsync(InfrastructureEntity entity)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/configuration/check?{entity.Type}={Uri.EscapeDataString(entity.Name)}";
                this.LogRequest(url);
                using (var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false))
                {
                    await HandleError(response).ConfigureAwait(false);
                }
            }
        }

        public async Task<ConfigurationStatusJsonModel> GetConfigurationStatusAsync(InfrastructureEntity entity)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/configuration/status?{entity.Type}={Uri.EscapeDataString(entity.Name)}";
                this.LogRequest(url);

                using (var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false))
                {
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
        }

        public async Task<string> TriggerRemediationJobAsync(InfrastructureEntity entity, string jobName = null)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/configuration/remediate/{entity.Type}/{Uri.EscapeDataString(entity.Name)}";
                if (jobName != null)
                    url += $"?job={jobName}";

                using (var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false))
                {
                    await HandleError(response).ConfigureAwait(false);

                    string jobToken = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return jobToken;
                }
            }
        }

        public async Task<RemediationStatus> GetRemediationJobStatusAsync(string jobToken)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/configuration/remediate/status?token={Uri.EscapeDataString(jobToken)}";
                this.LogRequest(url);
                using (var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false))
                {
                    await HandleError(response).ConfigureAwait(false);

                    string status = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    RemediationStatus result;
                    if (Enum.TryParse(status, ignoreCase: true, result: out result))
                        return result;
                    else
                        throw new OtterException(500, "Unexpected remediation job status returned from Otter: " + status);
                }
            }
        }

        public async Task<IList<string>> EnumerateInfrastructureAsync(string entityType)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/infrastructure/{entityType}s/list";
                this.LogRequest(url);
                var response = await client.GetAsync(url, this.cancellationToken).ConfigureAwait(false);
                await HandleError(response).ConfigureAwait(false);

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(reader)) 
                {
                    jsonReader.Read();
                    if (jsonReader.TokenType != JsonToken.StartArray)
                        throw new OtterException(400, $"Expected StartArray token from Otter Infrastructure API, was '{jsonReader.TokenType}'.");

                    var entities = new List<string>();

                    while (jsonReader.Read())
                    {
                        if (jsonReader.TokenType == JsonToken.PropertyName)
                        {
                            string value = jsonReader.Value.ToString();
                            if (string.Equals(value, "name", StringComparison.OrdinalIgnoreCase))
                            {
                                string entityName = jsonReader.ReadAsString();
                                entities.Add(entityName);
                            }
                            else
                            {
                                jsonReader.Skip();
                            }
                        }
                    }

                    return entities;
                }
            }
        }

        public async Task SetGlobalVariableAsync(string name, string value)
        {
            using (var client = this.CreateClient())
            {
                string url = $"api/variables/global/{Uri.EscapeDataString(name)}"; 

                this.LogRequest(url);
                
                using (var content = new StringContent(value))
                using (var response = await client.PostAsync(url, content).ConfigureAwait(false))
                {
                    await HandleError(response).ConfigureAwait(false);
                }
            }
        }

        public async Task SetSingleVariableAsync(ScopedVariableJsonModel variable)
        {
            if (string.IsNullOrEmpty(variable.Server) && string.IsNullOrEmpty(variable.ServerRole) && string.IsNullOrEmpty(variable.Environment))
                throw new InvalidOperationException("Specified variable requires at least one scope (server, role, or environment).");

            using (var client = this.CreateClient())
            {
                string url = "api/variables/scoped/single";
                this.LogRequest(url);
                using (var stream = new SlimMemoryStream())
                using (var writer = new StreamWriter(stream))
                {
                    JsonSerializer.CreateDefault().Serialize(writer, variable);
                    await writer.FlushAsync().ConfigureAwait(false);
                    stream.Position = 0;

                    using (var content = new StreamContent(stream))
                    using (var response = await client.PostAsync(url, content).ConfigureAwait(false))
                    {
                        await HandleError(response).ConfigureAwait(false);
                    }
                }
            }
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
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            this.Name = name;
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
            public override string Type => InfrastructureEntity.Server;
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
