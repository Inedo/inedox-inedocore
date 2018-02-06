using System.ComponentModel;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
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
