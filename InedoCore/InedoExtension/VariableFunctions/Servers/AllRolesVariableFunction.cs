using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

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
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class AllRolesVariableFunction : VectorVariableFunction
    {
        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            return SDK.GetServerRoles().Select(r => r.Name);
        }
    }
}
