using System.ComponentModel;
using System.Linq;
using Inedo.Extensibility;
using Inedo.Documentation;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("ServerName")]
    [Description("name of the current server in context")]
    [Tag("servers")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class ServerNameVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            if (context.ServerId != null)
                return SDK.GetServers(true).FirstOrDefault(s => s.Id == context.ServerId)?.Name;
            else
                return string.Empty;
        }
    }
}
