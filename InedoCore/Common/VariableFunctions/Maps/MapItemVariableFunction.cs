using Inedo.Documentation;
using Inedo.ExecutionEngine;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using System.Collections.Generic;
using System.ComponentModel;

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

#if BuildMaster
        public override RuntimeValue Evaluate(IGenericBuildMasterContext context)
#elif Hedgehog
        public override RuntimeValue Evaluate(IVariableFunctionContext context)
#elif Otter
        public override RuntimeValue Evaluate(IOtterContext context)
#endif
        {
            return this.Map[this.Key];
        }
    }
}
