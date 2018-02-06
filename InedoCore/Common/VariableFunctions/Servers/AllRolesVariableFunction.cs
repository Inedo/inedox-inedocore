using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;

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
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
    public sealed class AllRolesVariableFunction : CommonVectorVariableFunction
    {
        protected override IEnumerable EvaluateVector(object context)
        {
            return SDK.GetServerRoles().Select(r => r.Name);
        }
    }
}
