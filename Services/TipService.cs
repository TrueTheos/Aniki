using Aniki.Services.Save;
using Aniki.Views;
using Avalonia.Controls;

namespace Aniki.Services;

internal static class TipService
{
    private const string FileName = "tips.json";
    private static readonly SaveEntity<HashSet<string>> Saver = new(SaveService.MainDirectory);
    private static readonly HashSet<string> Seen = Saver.Read(FileName) ?? [];
    private static bool _showing;

    public static async Task Show(string id, string message, Control anchor)
    {
        if (_showing || !anchor.IsEffectivelyVisible || TopLevel.GetTopLevel(anchor) is null || !Seen.Add(id)) return;
        Saver.Save(FileName, Seen);
        _showing = true;
        try
        {
            await ToastService.Show(new TipView(message, anchor), 12000).ConfigureAwait(true);
        }
        finally
        {
            _showing = false;
        }
    }
}
