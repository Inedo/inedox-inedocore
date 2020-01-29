using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

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
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class AllEnvironmentsVariableFunction : VectorVariableFunction
    {
        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            return SDK.GetEnvironments().Select(e => e.Name);
        }
    }
}
