using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Maps
{
    [ScriptAlias("MapRemove")]
    [Description("Removes a key from a map.")]
    [Tag("maps")]
    public sealed class MapRemoveVariableFunction : VariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("map")]
        [Description("The map.")]
        public IDictionary<string, RuntimeValue> Map { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("key")]
        [Description("The key to remove.")]
        public string Key { get; set; }

        public override RuntimeValue Evaluate(IVariableFunctionContext context)
        {
            var value = this.Map
                .Where(kv => kv.Key != this.Key)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return new RuntimeValue(value);
        }
    }
}
