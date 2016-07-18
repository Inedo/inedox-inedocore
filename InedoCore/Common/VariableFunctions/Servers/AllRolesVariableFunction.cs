using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
#elif BuildMaster
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
#endif

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("AllRoles")]
    [Description("Returns a list of all server roles.")]
    [Tag("servers")]
    [Example(@"
# log all server roles in context to the execution log
foreach $Role in @AllRoles
{
    Log-Information $Role;
}
")]
    public sealed class AllRolesVariableFunction : CommonVectorVariableFunction
    {
        protected override IEnumerable EvaluateVector(object context) => DB.ServerRoles_GetServerRoles().Select(s => s.ServerRole_Name);
    }
}
