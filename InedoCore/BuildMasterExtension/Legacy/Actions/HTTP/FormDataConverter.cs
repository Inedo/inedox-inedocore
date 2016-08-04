using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.HTTP
{
    public sealed class FormDataConverter : ICustomPersistentSerializer, ICustomVariableReplacer
    {
        object ICustomPersistentSerializer.Serialize(object instance)
        {
            var items = (IList<KeyValuePair<string, string>>)instance;

            return from i in items
                   select new XElement("Field", new XAttribute("Name", i.Key), i.Value ?? string.Empty);
        }
        object ICustomPersistentSerializer.Deserialize(XElement element)
        {
            if (element == null)
                return null;

            return (from e in element.Elements("Field")
                    select new KeyValuePair<string, string>((string)e.Attribute("Name"), (string)e)).ToList();
        }

        IEnumerable<VariableExpandingField> ICustomVariableReplacer.GetFieldsToExpand(object instance)
        {
            var items = (IList<KeyValuePair<string, string>>)instance;
            if (items == null)
                yield break;

            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var key = items[index].Key;
                var value = items[index].Value;
                yield return new VariableExpandingField(items[index].Key, v => items[index] = new KeyValuePair<string, string>(v, value));
                yield return new VariableExpandingField(items[index].Value, v => items[index] = new KeyValuePair<string, string>(items[index].Key, v));
            }
        }
    }
}
