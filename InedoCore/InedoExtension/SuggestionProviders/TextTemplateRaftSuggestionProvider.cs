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
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            using (var raft = RaftRepository.OpenRaft(RaftRepository.DefaultName))
            {
                if (raft == null)
                    return Enumerable.Empty<string>();

                var items = await raft.GetRaftItemsAsync(RaftItemType.TextTemplate).ConfigureAwait(false);

                return items
                    .Select(i => i.ItemName)
                    .ToList();
            }
        }
    }
}
