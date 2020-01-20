using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("ServersInRole")]
    [Description("Returns a list of servers in the specified role.")]
    [Tag("servers")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class ServersInRoleVariableFunction : VectorVariableFunction
    {
        [DisplayName("roleName")]
        [VariableFunctionParameter(0, Optional = true)]
        [Description("The name of the server role. If not supplied, the current role in context will be used.")]
        public string RoleName { get; set; }

        [DisplayName("includeInactive")]
        [VariableFunctionParameter(1, Optional = true)]
        [Description("If true, include servers marked as inactive.")]
        public bool IncludeInactive { get; set; }

        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            int? roleId = FindRole(this.RoleName, context);
            if (roleId == null)
                return null;
            
            return SDK.GetServersInRole(roleId.Value)
                .Where(s => this.IncludeInactive || s.Active)
                .Select(s => s.Name);
        }
        
        private int? FindRole(string roleName, IVariableFunctionContext context)
        {
            var allRoles = SDK.GetServerRoles();

            if (!string.IsNullOrEmpty(roleName))
            {
                return allRoles
                    .FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase))
                    ?.Id;
            }
            else
            {
                return context.ServerRoleId;
            }
        }
    }
}
