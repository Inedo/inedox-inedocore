using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.RaftRepositories;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.RaftRepositories;
using Inedo.Otter.Web.Controls;
#endif

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class TextTemplateRaftSuggestionProvider : ISuggestionProvider
    {
        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            using (var raft = RaftRepository.OpenRaft(RaftRepository.DefaultName))
            {
                if (raft == null)
                    return Task.FromResult(Enumerable.Empty<string>());

                return Task.FromResult<IEnumerable<string>>(
                    raft.GetRaftItems(RaftItemType.TextTemplate)
                        .Select(i => i.ItemName)
                        .ToList()
                );
            }
        }
    }
}
