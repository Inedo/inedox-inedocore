using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

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
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
    public sealed class AllServersVariableFunction : CommonVectorVariableFunction
    {
        [DisplayName("includeInactive")]
        [VariableFunctionParameter(0, Optional = true)]
        [Description("If true, include servers marked as inactive.")]
        public bool IncludeInactive { get; set; }

        protected override IEnumerable EvaluateVector(object context)
        {
            return SDK.GetServers(this.IncludeInactive).Select(s => s.Name);
        }
    }
}
