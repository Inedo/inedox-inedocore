using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("AllEnvironments")]
    [Description("Returns a list of all environments.")]
    [Tag("servers")]
    [Example(@"
# log all environments in context to the execution log
foreach $Env in @AllEnvironments
{
    Log-Information $Env;
}
")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
    public sealed class AllEnvironmentsVariableFunction : CommonVectorVariableFunction
    {
        protected override IEnumerable EvaluateVector(object context)
        {
            return SDK.GetEnvironments().Select(e => e.Name);
        }
    }
}
