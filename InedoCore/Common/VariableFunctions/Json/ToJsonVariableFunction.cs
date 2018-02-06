using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Documentation;
using Inedo.ExecutionEngine;

namespace Inedo.Extensions.VariableFunctions.Json
{
    [ScriptAlias("ToJson")]
    [Description("Converts an OtterScript value to JSON.")]
    [Example(@"$ToJson(%(abc: @(1, 2, 3), def: true))")]
    [Example(@"$ToJson(@(1, 2, 3, 4, five, 6, 7, 8, 9))")]
    [Example(@"$ToJson(>>abc
123>>)")]
    [Note("Maps are converted to objects, vectors are converted to arrays, and all other values are converted to strings.")]
    [Tag("json")]
    public sealed class ToJsonVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("data")]
        [Description("The data to encode as JSON.")]
        public RuntimeValue Data { get; set; }

        protected override object EvaluateScalar(object context)
        {
            using (var writer = new StringWriter())
            {
                using (var json = new JsonTextWriter(writer) { CloseOutput = false })
                {
                    WriteJson(json, this.Data);
                }
                return writer.ToString();
            }
        }

        private static void WriteJson(JsonTextWriter json, RuntimeValue data)
        {
            switch (data.ValueType)
            {
                case RuntimeValueType.Scalar:
                    json.WriteValue(data.AsString());
                    break;
                case RuntimeValueType.Vector:
                    json.WriteStartArray();
                    foreach (var v in data.AsEnumerable())
                    {
                        WriteJson(json, v);
                    }
                    json.WriteEndArray();
                    break;
                case RuntimeValueType.Map:
                    json.WriteStartObject();
                    foreach (var v in data.AsDictionary())
                    {
                        json.WritePropertyName(v.Key);
                        WriteJson(json, v.Value);
                    }
                    json.WriteEndObject();
                    break;
            }
        }
    }
}
