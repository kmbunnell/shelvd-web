using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.Tags;

public sealed class TagsLoader(ITagsCache tagsCache) : IDisposable
{
    private CancellationTokenSource? _cts;

    public Result<IReadOnlyList<TagDto>, TagsApiError>? Result { get; private set; }

    public async Task LoadAsync(Action onLoaded)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            Result = await tagsCache.GetTagsAsync(_cts.Token);
            onLoaded();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a subsequent LoadAsync call (retry) or the component
            // was disposed before the fetch completed — either way, nothing to render.
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
