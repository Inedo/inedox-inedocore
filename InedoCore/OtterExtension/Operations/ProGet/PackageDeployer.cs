using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet
{
    partial class PackageDeployer
    {
        private static Task RecordServerPackageInfoAsync(IOperationExecutionContext context, string name, string version, string url, ILogger log)
        {
            log.LogDebug("Recording server package information...");
            return new DB.Context(false).ServerPackages_CreateOrUpdatePackageAsync(
                Server_Id: context.ServerId,
                PackageType_Name: "ProGet",
                Package_Name: name,
                Package_Version: version,
                CollectedOn_Execution_Id: context.ExecutionId,
                Url_Text: url,
                CollectedFor_ServerRole_Id: context.ServerRoleId
            );
        }
    }
}
