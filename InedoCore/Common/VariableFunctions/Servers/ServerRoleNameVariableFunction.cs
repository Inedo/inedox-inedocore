using System.ComponentModel;
using System.Linq;
#if Otter
using Inedo.Otter;
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("RoleName")]
    [Description("name of the current server role in context")]
    [Tag("servers")]
    public sealed class ServerRoleNameVariableFunction : CommonScalarVariableFunction
    {
        protected override object EvaluateScalar(object context)
        {
            int? serverRoleId =
#if BuildMaster
                (context as IGenericBuildMasterContext)
#elif Otter
                (context as IOtterContext)
#endif
                ?.ServerRoleId;

            if (serverRoleId != null)
            {
                return DB.ServerRoles_GetServerRole(serverRoleId)?.ServerRole_Name;
            }

            return string.Empty;
        }
    }
}