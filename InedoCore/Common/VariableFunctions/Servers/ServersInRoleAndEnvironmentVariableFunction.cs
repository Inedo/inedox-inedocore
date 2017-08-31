using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
#if Otter
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
using ContextType = Inedo.Otter.IOtterContext;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Data;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.VariableFunctions;
using ContextType = Inedo.Hedgehog.IHedgehogContext;
#elif BuildMaster
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
using ContextType = Inedo.BuildMaster.Extensibility.IGenericBuildMasterContext;
#endif

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("ServersInRoleAndEnvironment")]
    [Description("Returns a list of all the servers in the specified role and environment name.")]
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

        protected override IEnumerable EvaluateVector(ContextType context)
        {
            int? roleId = FindRole(this.RoleName, context);
            if (roleId == null)
                return null;

            int? environmentId = FindEnvironment(this.EnvironmentName, context);
            if (environmentId == null)
                return null;

            return DB.Servers_SearchServers(Has_ServerRole_Id: roleId, In_Environment_Id: environmentId)
#if Otter || Hedgehog
                .Servers_Extended
#elif BuildMaster
                .Servers
#endif
                .Where(s => s.Active_Indicator || this.IncludeInactive)
                .Select(s => s.Server_Name);
        }

        private int? FindRole(string roleName, ContextType context)
        {
            var allRoles = DB.ServerRoles_GetServerRoles();

            if (!string.IsNullOrEmpty(roleName))
            {
                return allRoles
                    .FirstOrDefault(r => r.ServerRole_Name.Equals(roleName, StringComparison.OrdinalIgnoreCase))
                    ?.ServerRole_Id;
            }
            else
            {
                return allRoles
                    .FirstOrDefault(r => r.ServerRole_Id == context.ServerRoleId)
                    ?.ServerRole_Id;
            }
        }

        private int? FindEnvironment(string environmentName, ContextType context)
        {
            var allEnvironments = DB.Environments_GetEnvironments();

            if (!string.IsNullOrEmpty(environmentName))
            {
                return allEnvironments
                    .FirstOrDefault(e => e.Environment_Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase))
                    ?.Environment_Id;
            }
            else
            {
                return allEnvironments
                    .FirstOrDefault(e => e.Environment_Id == context.EnvironmentId)
                    ?.Environment_Id;
            }
        }
    }
}
