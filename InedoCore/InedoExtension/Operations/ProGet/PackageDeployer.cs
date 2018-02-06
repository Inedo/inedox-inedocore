using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet
{
    partial class PackageDeployer
    {
        private static Task RecordServerPackageInfoAsync(IOperationExecutionContext context, string name, string version, string url, ILogSink log) => Task.FromResult<object>(null);
    }
}
