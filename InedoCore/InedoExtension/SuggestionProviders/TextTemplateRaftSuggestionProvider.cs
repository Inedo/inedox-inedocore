using Inedo.Extensibility.RaftRepositories;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class TextTemplateRaftSuggestionProvider : ISuggestionProvider
    {
        public IAsyncEnumerable<string> GetSuggestionsAsync(IComponentConfiguration config, CancellationToken cancellationToken)
        {
            return SDK.GetRaftItems(RaftItemType.TextFile, config.EditorContext).Select(i => i.Id).ToAsyncEnumerable();
        }
    }
}
