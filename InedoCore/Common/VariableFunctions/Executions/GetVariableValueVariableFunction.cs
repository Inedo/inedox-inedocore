using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
#if Otter
using Inedo.Otter;
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
    [ScriptAlias("GetVariableValue")]
    [Description("Returns the value of a variable if the specified variable name is available in the current context; otherwise returns null.")]
    [Tag("executions")]
    public sealed class GetVariableValueVariableFunction : VariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("name")]
        [Description("The name of the variable.")]
        public string VariableName { get; set; }

        [VariableFunctionParameter(1, Optional = true)]
        [DisplayName("type")]
        [Description("Must be one of: any, scalar, vector, or map; when none is specified, \"any\" is used.")]
        public string VariableType { get; set; }
#if BuildMaster
        public override RuntimeValue Evaluate(IGenericBuildMasterContext context)
#elif Otter
        public override RuntimeValue Evaluate(IOtterContext context)
#endif
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
            if (execContext == null)
                return null;

            foreach (var type in types)
            {
                var variableName = new RuntimeVariableName(this.VariableName, type);
                var value = execContext.TryGetVariableValue(variableName);
                if (value != null)
                    return value.Value;
#if Otter
                var functionValue = execContext.TryGetFunctionValue(variableName.ToString());
                if (functionValue != null)
                    return functionValue.Value;
#elif BuildMaster
                var functionValue = execContext.TryGetFunctionValue(variableName.Name, new RuntimeValue[0]);
                if (functionValue != null)
                    return functionValue.Value;
#endif
            }

            return null;
        }
    }
}
