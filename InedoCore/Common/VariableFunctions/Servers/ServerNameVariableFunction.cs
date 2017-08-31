using System.ComponentModel;
using System.Linq;
#if Otter
using Inedo.Otter;
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Data;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Operations;
using Inedo.Hedgehog.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("ServerName")]
    [Description("name of the current server in context")]
#if BuildMaster
    [LegacyAlias("SVRNAME")]
#endif 
    [Tag("servers")]
    public sealed class ServerNameVariableFunction : CommonScalarVariableFunction
    {
        protected override object EvaluateScalar(object context)
        {
            int? serverId =
#if BuildMaster
                (context as IGenericBuildMasterContext)
#elif Hedgehog
                (context as IHedgehogContext)
#elif Otter
                (context as IOtterContext)
#endif
                ?.ServerId;

            if (serverId != null)
            {
                return DB.Servers_GetServer(serverId)
#if BuildMaster
                .Servers
#elif Otter || Hedgehog
                .Servers_Extended
#endif
                .FirstOrDefault()?.Server_Name;
            }

            return string.Empty;
        }
    }
}
