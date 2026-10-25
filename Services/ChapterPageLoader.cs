using System.Collections.Concurrent;
using AsyncImageLoader.Loaders;
using Avalonia.Media.Imaging;

namespace Aniki.Services;

internal sealed class ChapterPageLoader : BaseWebImageLoader
{
    public static ChapterPageLoader Instance { get; } = new();

    private ChapterPageLoader()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Aniki");
    }

    private readonly SemaphoreSlim _slots = new(4);
    private ConcurrentBag<Bitmap> _chapter = [];

    public override async Task<Bitmap?> ProvideImageAsync(string url)
    {
        ConcurrentBag<Bitmap> chapter = _chapter;
        await _slots.WaitAsync().ConfigureAwait(false);
        try
        {
            if (chapter != _chapter) return null;
            Bitmap? bitmap = await base.ProvideImageAsync(url).ConfigureAwait(false);
            if (bitmap == null) return null;
            if (chapter != _chapter)
            {
                bitmap.Dispose();
                return null;
            }
            chapter.Add(bitmap);
            return bitmap;
        }
        finally
        {
            _slots.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _slots.Dispose();
        base.Dispose(disposing);
    }

    public void ReleaseChapter()
    {
        foreach (Bitmap bitmap in Interlocked.Exchange(ref _chapter, [])) bitmap.Dispose();
    }
}
