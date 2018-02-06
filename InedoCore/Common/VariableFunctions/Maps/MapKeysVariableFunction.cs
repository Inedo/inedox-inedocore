using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;


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
