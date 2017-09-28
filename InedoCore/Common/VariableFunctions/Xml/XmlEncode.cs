using System.ComponentModel;
using System.Xml.Linq;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Xml
{
    [ScriptAlias("XmlEncode")]
    [Description("Encodes a string for use in an XML element.")]
    [Tag("xml")]
    public sealed class XmlEncode : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The text to encode.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(object context) => new XText(this.Text ?? string.Empty).ToString(SaveOptions.DisableFormatting);
    }
}
