using System.Collections;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

#nullable enable

namespace Inedo.Extensions.VariableFunctions.Lists
{
    [ScriptAlias("ListSet")]
    [Description("Updates the value at a given position in the list to a new value.")]
    [Tag("lists")]
    public sealed class ListSetVariableFunction : VectorVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("list")]
        [Description("The list to update")]
        public IEnumerable<RuntimeValue>? List { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("index")]
        [Description("The 0-based index to set.  If negative, counts from the end of the list.  If positive and larger than the list, grows the list as necessary.")]
        public int Index { get; set; }

        [VariableFunctionParameter(2)]
        [DisplayName("item")]
        [Description("The new value")]
        public RuntimeValue Item { get; set; }

        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            if (this.List is null) throw new ArgumentNullException(nameof(this.List));

            var list = this.List.ToList();
            var index = this.Index;

            // bounds checking
            if (index >= list.Count)
            {
                // allow for growing the list to fit new index
                var empty = RuntimeValue.Default(RuntimeValueType.Scalar);
                list.AddRange(Enumerable.Range(0, 1+index-list.Count)
                                        .Select(_ => empty));
            }
            else if (index < 0)
            {
                // allow for negative indexing from end of array (but not growth)
                if (-index >= list.Count) throw new ArgumentOutOfRangeException(nameof(this.Index));
                index = list.Count + (index % list.Count);
            }

            list[index] = this.Item;
            return list;
        }
    }
}