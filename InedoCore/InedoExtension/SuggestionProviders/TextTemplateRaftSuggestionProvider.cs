using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class TextTemplateRaftSuggestionProvider : ISuggestionProvider
    {
        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            return Task.FromResult(SDK.GetRaftItems(RaftItemType.TextTemplate, null).Select(i => i.Name));
        }
    }
}
