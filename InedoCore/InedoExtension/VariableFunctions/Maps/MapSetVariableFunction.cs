using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Maps
{
    [ScriptAlias("MapSet")]
    [Description("Adds a key-value pair to a map.")]
    [Tag("maps")]
    public sealed class MapSetVariableFunction : VariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("map")]
        [Description("The map.")]
        public IDictionary<string, RuntimeValue>? Map { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("key")]
        [Description("The key to set.")]
        public string? Key { get; set; }

        [VariableFunctionParameter(2)]
        [DisplayName("value")]
        [Description("The value to add.")]
        public RuntimeValue Value { get; set; }

        public override RuntimeValue Evaluate(IVariableFunctionContext context)
        {
            if (this.Map is null) throw new ArgumentNullException(nameof(this.Map));
            if (String.IsNullOrWhiteSpace(this.Key)) throw new ArgumentNullException(nameof(this.Key));

            var map = new Dictionary<string, RuntimeValue>(this.Map);
            map[this.Key] = this.Value;

            return new RuntimeValue(map);
        }
    }
}