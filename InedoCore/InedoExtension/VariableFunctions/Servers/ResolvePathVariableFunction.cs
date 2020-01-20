using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
using Inedo.IO;

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("ResolvePath")]
    [Description("Provides an absolute path (terminated with directory separator) based on a relative path and the current working directory; "
        + "this will provide appropriate directory separators, based on the server in context")]
    [Example(@"$ResolvePath(C:\MyDirectory) -> C:\MyDirectory\
$ResolvePath() -> {WorkingDirectory}
$ResolvePath(my\path/to/directory) -> {WorkingDirectory}/my/path/to/directory (on Linux)
$ResolvePath(my\path/to/directory) -> {WorkingDirectory}\my\path\to\directory (on Windows)
$ResolvePath(~\path) -> {ExecutionDirectory}\path")]
    [Tag("files")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class ResolvePathVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("path")]
        public string Path { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            if (!(context is IOperationExecutionContext execContext))
                throw new InvalidOperationException("$ResolvePath requires an execution context.");

            return PathEx.EnsureTrailingDirectorySeparator(execContext.ResolvePath(this.Path));
        }
    }
}
