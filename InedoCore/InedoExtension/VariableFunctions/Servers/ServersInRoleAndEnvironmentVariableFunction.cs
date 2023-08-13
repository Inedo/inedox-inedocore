using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("ServersInRoleAndEnvironment")]
    [Description("Returns a list of all the servers in the specified role and environment name.")]
    [Tag("servers")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    [Note("When the specified environment contains child environments, all servers in the heirarchy will be returned.")]
    public sealed class ServersInRoleAndEnvironmentVariableFunction : VectorVariableFunction
    {
        [DisplayName("roleName")]
        [VariableFunctionParameter(0, Optional = true)]
        [Description("The name of the server role. If not supplied, the current role in context will be used.")]
        public string RoleName { get; set; }

        [DisplayName("environmentName")]
        [VariableFunctionParameter(1, Optional = true)]
        [Description("The name of the evironment. If not supplied, the current environment in context will be used.")]
        public string EnvironmentName { get; set; }

        [DisplayName("includeInactive")]
        [VariableFunctionParameter(2, Optional = true)]
        [Description("If true, include servers marked as inactive.")]
        public bool IncludeInactive { get; set; }

        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            int? roleId = FindRole(this.RoleName, context);
            if (roleId == null)
                return null;

            int? environmentId = FindEnvironment(this.EnvironmentName, context);
            if (environmentId == null)
                return null;

            return from s in SDK.GetServersInRole(roleId.Value)
                   where this.IncludeInactive || s.Active
                   join e in SDK.GetServersInEnvironment(environmentId.Value) on s.Id equals e.Id
                   select s.Name;
        }

        private int? FindRole(string roleName, IVariableFunctionContext context)
        {
            var allRoles = SDK.GetServerRoles();
            if (!string.IsNullOrEmpty(roleName))
                return allRoles.FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase))?.Id;
            else
                return context.ServerRoleId;
        }

        private int? FindEnvironment(string environmentName, IVariableFunctionContext context)
        {
            var allEnvironments = SDK.GetEnvironments();
            if (!string.IsNullOrEmpty(environmentName))
                return allEnvironments.FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase))?.Id;
            else
                return context.EnvironmentId;
        }
    }
}
