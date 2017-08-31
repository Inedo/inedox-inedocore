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
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Configurations;
using Inedo.Hedgehog.Extensibility.Credentials;
using Inedo.Hedgehog.Extensibility.Operations;
using Inedo.Hedgehog.Extensibility.RaftRepositories;
using Inedo.Hedgehog.Web;
using Inedo.Hedgehog.Web.Controls;
using Inedo.Hedgehog.Web.Controls.Plans;
#endif

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class TextTemplateRaftSuggestionProvider : ISuggestionProvider
    {
        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
#if Hedgehog
            throw new System.NotImplementedException();
#else
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
#endif
        }
    }
}
