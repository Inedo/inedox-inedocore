using System.ComponentModel;
using Inedo.Agents;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("EnvironmentVariable")]
    [Description("Returns the value of the specified environment variable on the current server.")]
    [Tag("servers")]
    [Example(@"
# get the PATH on the server in context during an execution
set $Path = $EnvironmentVariable(PATH);
Log-Information $Path;
")]
    public sealed class EnvironmentVariableVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [Description("The name of the environment variable.")]
        public string EnvironmentVariableName { get; set; }

        protected override object EvaluateScalar(object context)
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
                throw new VariableFunctionException("Execution context is not available.");

            var remote = execContext.Agent.GetService<IRemoteProcessExecuter>();
            return remote.GetEnvironmentVariableValue(this.EnvironmentVariableName) ?? string.Empty;
        }
    }
}
