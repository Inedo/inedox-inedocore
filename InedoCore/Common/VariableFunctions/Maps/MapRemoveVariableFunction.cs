using Inedo.Documentation;
using Inedo.ExecutionEngine;
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Inedo.Extensions.VariableFunctions.Maps
{
    [ScriptAlias("MapRemove")]
    [Description("Removes a key from a map.")]
    [Tag("maps")]
    public sealed class MapRemoveVariableFunction : CommonMapVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("map")]
        [Description("The map.")]
        public IDictionary<string, RuntimeValue> Map { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("key")]
        [Description("The key to remove.")]
        public string Key { get; set; }

        protected override IDictionary<string, RuntimeValue> EvaluateMap(object context) => this.Map
            .Where(kv => kv.Key != this.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
