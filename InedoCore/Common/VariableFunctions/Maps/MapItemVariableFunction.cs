using System.Collections.Generic;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;


namespace Inedo.Extensions.VariableFunctions.Maps
{
    [ScriptAlias("MapItem")]
    [Description("Gets an item from a map.")]
    [Tag("maps")]
    public sealed class MapItemVariableFunction : VariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("map")]
        [Description("The map.")]
        public IDictionary<string, RuntimeValue> Map { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("key")]
        [Description("The key.")]
        public string Key { get; set; }

        public override RuntimeValue Evaluate(IVariableFunctionContext context)
        {
            return this.Map[this.Key];
        }
    }
}
