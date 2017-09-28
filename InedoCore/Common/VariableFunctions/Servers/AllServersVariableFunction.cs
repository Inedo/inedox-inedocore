using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
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
#if Hedgehog
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
#endif
    public sealed class AllServersVariableFunction : CommonVectorVariableFunction
    {
        [DisplayName("includeInactive")]
        [VariableFunctionParameter(0, Optional = true)]
        [Description("If true, include servers marked as inactive.")]
        public bool IncludeInactive { get; set; }

        protected override IEnumerable EvaluateVector(object context)
        {
#if Hedgehog
            return SDK.GetServers(this.IncludeInactive).Select(s => s.Name);
#else
            return DB.Servers_GetServers(IncludeInactive_Indicator: this.IncludeInactive).Select(s => s.Server_Name);
#endif
        }
    }
}
