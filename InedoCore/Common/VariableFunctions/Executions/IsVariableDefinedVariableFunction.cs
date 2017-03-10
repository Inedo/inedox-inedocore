using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using System;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Executions
{
    [ScriptAlias("IsVariableDefined")]
    [Description("Returns true if the specified variable name is available in the current context; otherwise returns false.")]
    [Tag("executions")]
    public sealed class IsVariableDefinedVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("name")]
        [Description("The name of the variable.")]
        public string VariableName { get; set; }

        [VariableFunctionParameter(1, Optional = true)]
        [DisplayName("type")]
        [Description("Must be one of: any, scalar, vector, or map; when none is specified, \"any\" is used.")]
        public string VariableType { get; set; }

        protected override object EvaluateScalar(object context)
        {
            if (string.IsNullOrEmpty(this.VariableName))
                return false;

            RuntimeValueType[] types;
            if (string.Equals(this.VariableType, "scalar", StringComparison.OrdinalIgnoreCase))
                types = new[] { RuntimeValueType.Scalar };
            else if (string.Equals(this.VariableType, "vector", StringComparison.OrdinalIgnoreCase))
                types = new[] { RuntimeValueType.Vector };
            else if (string.Equals(this.VariableType, "map", StringComparison.OrdinalIgnoreCase))
                types = new[] { RuntimeValueType.Map };
            else
                types = new[] { RuntimeValueType.Scalar, RuntimeValueType.Vector, RuntimeValueType.Map };

            var execContext = context as IOperationExecutionContext;

            foreach (var type in types)
            {
                var variableName = new RuntimeVariableName(this.VariableName, type);
                if (execContext.TryGetVariableValue(variableName) != null)
                    return true;

#if Otter
                if (execContext.TryGetFunctionValue(variableName.ToString()) != null)
                    return true;
#elif BuildMaster
                if (execContext.TryGetFunctionValue(variableName.Name, new RuntimeValue[0]) != null)
                    return true;
#endif
            }

            return false;
        }
    }
}
