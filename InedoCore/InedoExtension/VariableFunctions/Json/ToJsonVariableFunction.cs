using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

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
    public sealed class ToJsonVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("data")]
        [Description("The data to encode as JSON.")]
        public RuntimeValue Data { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteJson(writer, this.Data);
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream, InedoLib.UTF8Encoding);
            return reader.ReadToEnd();
        }

        private static void WriteJson(Utf8JsonWriter json, RuntimeValue data)
        {
            switch (data.ValueType)
            {
                case RuntimeValueType.Scalar:
                    json.WriteStringValue(data.AsString());
                    break;
                case RuntimeValueType.Vector:
                    json.WriteStartArray();
                    foreach (var v in data.AsEnumerable())
                        WriteJson(json, v);
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
