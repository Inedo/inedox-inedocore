using Inedo.Documentation;
using Inedo.ExecutionEngine;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;

namespace Inedo.Extensions.VariableFunctions.Maps
{
    [ScriptAlias("MapKeys")]
    [Description("Lists the keys of a map.")]
    [Tag("maps")]
    public sealed class MapKeysVariableFunction : CommonVectorVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("map")]
        [Description("The map.")]
        public IDictionary<string, RuntimeValue> Map { get; set; }

        protected override IEnumerable EvaluateVector(object context) => this.Map.Keys;
    }
}
