using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Operations.ProGet
{
    partial class PackageDeployer
    {
        private static Task RecordServerPackageInfoAsync(IOperationExecutionContext context, string name, string version, string url, ILogger log) => Task.FromResult<object>(null);
    }
}
