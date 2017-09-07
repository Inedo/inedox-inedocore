using System.ComponentModel;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    [ScriptAlias("WorkingDirectory")]
    [ScriptAlias("CurrentDirectory")]
    [Description("Returns the current working directory.")]
#if BuildMaster
    [Note("This is the equivalent value of $SourceDirectory for a legacy action.")]
#endif
    [Tag("executions")]
    public sealed partial class WorkingDirectoryVariableFunction : ScalarVariableFunction
    {
    }
}
