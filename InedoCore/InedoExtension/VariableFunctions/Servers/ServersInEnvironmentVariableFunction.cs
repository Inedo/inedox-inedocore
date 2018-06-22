using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("ServersInEnvironment")]
    [Tag("servers")]
    [Description("Returns a list of all the servers in the specified environment name.")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
    public sealed class ServersInEnvironmentVariableFunction : VectorVariableFunction
    {
        [DisplayName("environmentName")]
        [VariableFunctionParameter(0, Optional = true)]
        [Description("The name of the evironment. If not supplied, the current environment in context will be used.")]
        public string EnvironmentName { get; set; }

        [DisplayName("includeInactive")]
        [VariableFunctionParameter(1, Optional = true)]
        [Description("If true, include servers marked as inactive.")]
        public bool IncludeInactive { get; set; }

        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            int? environmentId = FindEnvironment(this.EnvironmentName, context);
            if (environmentId == null)
                return null;

            return SDK.GetServersInEnvironment(environmentId.Value)
                .Where(s => this.IncludeInactive || s.Active)
                .Select(s => s.Name);
        }

        private int? FindEnvironment(string environmentName, IVariableFunctionContext context)
        {
            var allEnvironments = SDK.GetEnvironments();

            if (!string.IsNullOrEmpty(environmentName))
            {
                return allEnvironments
                    .FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase))
                    ?.Id;
            }
            else
            {
                return allEnvironments
                    .FirstOrDefault(e => e.Id == context.EnvironmentId)
                    ?.Id;
            }
        }
    }
}
