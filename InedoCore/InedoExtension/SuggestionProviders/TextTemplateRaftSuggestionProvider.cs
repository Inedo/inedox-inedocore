using Inedo.Extensibility.RaftRepositories;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class TextTemplateRaftSuggestionProvider : ISuggestionProvider
    {
        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            return Task.FromResult(SDK.GetRaftItems(RaftItemType.TextFile, config.EditorContext).Select(i => i.Id));
        }
    }
}
