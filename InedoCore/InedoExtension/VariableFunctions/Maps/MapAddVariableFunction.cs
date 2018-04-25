using System.Collections.Generic;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Maps
{
    [ScriptAlias("MapAdd")]
    [Description("Adds a key-value pair to a map.")]
    [Tag("maps")]
    public sealed class MapAddVariableFunction : VariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("map")]
        [Description("The map.")]
        public IDictionary<string, RuntimeValue> Map { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("key")]
        [Description("The key to add.")]
        public string Key { get; set; }

        [VariableFunctionParameter(2)]
        [DisplayName("value")]
        [Description("The value to add.")]
        public RuntimeValue Value { get; set; }

        public override RuntimeValue Evaluate(IVariableFunctionContext context)
        {
            var map = new Dictionary<string, RuntimeValue>(this.Map);
            map.Add(this.Key, this.Value);
            return new RuntimeValue(map);
        }
    }
}
