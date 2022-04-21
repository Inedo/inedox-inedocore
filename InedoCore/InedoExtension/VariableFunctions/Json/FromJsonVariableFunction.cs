using System;
using System.Collections.Generic;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
using System.Text.Json.Nodes;

namespace Inedo.Extensions.VariableFunctions.Json
{
    [ScriptAlias("FromJson")]
    [Description("Converts JSON to an OtterScript value.")]
    [Example(@"%FromJson(>>{""abc"":[1,2,3],""def"":true}>>)")]
    [Example(@"@FromJson(>>[1,2,3,4,""five"",6,7,8,9]>>)")]
    [Example(@"$FromJson(>>""abc\n123"">>)")]
    [Note("Objects are converted to maps, arrays are converted to vectors, and all other values are converted to strings.")]
    [Tag("json")]
    public sealed class FromJsonVariableFunction : VariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("json")]
        [Description("The JSON data to parse.")]
        public string Json { get; set; }

        public override RuntimeValue Evaluate(IVariableFunctionContext context)
        {
            return ToRuntimeValue(JsonNode.Parse(this.Json));
        }

        private static RuntimeValue ToRuntimeValue(JsonNode json)
        {
            switch (json)
            {
                case JsonObject obj:
                    var map = new Dictionary<string, RuntimeValue>(obj.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (var p in obj)
                        map[p.Key] = ToRuntimeValue(p.Value);
                    return new RuntimeValue(map);
                
                case JsonArray arr:
                    var list = new List<RuntimeValue>(arr.Count);
                    foreach (var v in arr)
                        list.Add(ToRuntimeValue(v));
                    return new RuntimeValue(list);

                default:
                    return new RuntimeValue(json?.ToString());
            }
        }
    }
}
