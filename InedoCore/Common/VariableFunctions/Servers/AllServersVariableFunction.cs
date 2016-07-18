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
    [ScriptAlias("AllServers")]
    [Description("Returns a list of all servers.")]
    [Tag("servers")]
    [Example(@"
# log all servers in context to the execution log
foreach $Server in @AllServers
{
    Log-Information $Server;
}
")]
    public sealed class AllServersVariableFunction : CommonVectorVariableFunction
    {
        protected override IEnumerable EvaluateVector(object context) => DB.Servers_GetServers().Select(s => s.Server_Name);
    }
}
