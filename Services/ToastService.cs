using System.Globalization;
using Aniki.Views;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;

namespace Aniki.Services;

internal static class ToastService
{
    public static Task Show(string message, int durationMs = 2200) => Show(new ToastView(message), durationMs);

    public static async Task Show(Control toast, int durationMs)
    {
        Window? host = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;

        if (host is null) return;
        
        Visual?       visual       = host.Presenter as Visual ?? host.Content as Visual;
        AdornerLayer? adornerLayer = visual is not null ? AdornerLayer.GetAdornerLayer(visual) : null;
        if (adornerLayer is null) return;

        AdornerLayer.SetAdornedElement(toast, host);
        adornerLayer.Children.Add(toast);

        await Task.Delay(durationMs).ConfigureAwait(true);
        if (toast.Parent is null) return;

        // Fade out
        Animation fadeOut = new()
        {
            Duration = TimeSpan.FromMilliseconds(150),
            Children =
            {
                new KeyFrame { Cue = Cue.Parse("0%", CultureInfo.CurrentCulture),  Setters  = { new Setter(Visual.OpacityProperty, 1.0) } },
                new KeyFrame { Cue = Cue.Parse("100%", CultureInfo.CurrentCulture), Setters = { new Setter(Visual.OpacityProperty, 0.0) } }
            }
        };
        await fadeOut.RunAsync(toast).ConfigureAwait(true);
        adornerLayer.Children.Remove(toast);
    }
}