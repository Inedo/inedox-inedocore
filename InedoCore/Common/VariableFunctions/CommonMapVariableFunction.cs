using System;
using System.Collections.Generic;
using Inedo.ExecutionEngine;
#if Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions
{
    public abstract class CommonMapVariableFunction : VariableFunction
    {
        protected CommonMapVariableFunction()
        {
        }

#if Otter
        public sealed override RuntimeValue Evaluate(IOtterContext context) => new RuntimeValue(this.EvaluateMap(context));
#elif BuildMaster
        public sealed override RuntimeValue Evaluate(IGenericBuildMasterContext context) => new RuntimeValue(this.EvaluateMap(context));
#elif Hedgehog
        public sealed override RuntimeValue Evaluate(IHedgehogContext context) => new RuntimeValue(this.EvaluateMap(context));
#endif

        protected abstract IDictionary<string, RuntimeValue> EvaluateMap(object context);
    }
}
