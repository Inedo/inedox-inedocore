using System.ComponentModel;
using System.Linq;
using Inedo.Extensibility;
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("ServerName")]
    [Description("name of the current server in context")]
#if BuildMaster
    [BuildMaster.Extensibility.VariableFunctions.LegacyAlias("SVRNAME")]
#endif 
    [Tag("servers")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
    public sealed class ServerNameVariableFunction : CommonScalarVariableFunction
    {
        protected override object EvaluateScalar(object context)
        {
            int? serverId = (context as IStandardContext)?.ServerId;
            if (serverId != null)
                return SDK.GetServers(true).FirstOrDefault(s => s.Id == serverId)?.Name;
            else
                return string.Empty;
        }
    }
}
