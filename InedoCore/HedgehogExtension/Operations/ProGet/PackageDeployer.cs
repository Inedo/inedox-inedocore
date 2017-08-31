using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Hedgehog.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet
{
    partial class PackageDeployer
    {
        private static Task RecordServerPackageInfoAsync(IOperationExecutionContext context, string name, string version, string url, ILogger log) => Task.FromResult<object>(null);
    }
}
