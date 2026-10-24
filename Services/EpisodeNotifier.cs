using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;
using Aniki.Services.Save;
using CommunityToolkit.Mvvm.Messaging;
#if WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace Aniki.Services;

internal sealed class EpisodeNotifier(IAnimeService animeService, ICalendarService calendarService, ISaveService saveService) : IDisposable
{
    private const string LAST_CHECK_FILE = "lastEpisodeCheck.json";
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxLookback = TimeSpan.FromDays(3);

    private readonly SaveEntity<DateTime> _lastCheck = new(SaveService.MainDirectory);
    private readonly HttpClient _client = new();
    private bool _running;

    public async Task RunAsync()
    {
        if (_running) return;
        _running = true;

#if WINDOWS
        ToastNotificationManagerCompat.OnActivated += OnToastClicked;
#endif

        using PeriodicTimer timer = new(Interval);
        do
        {
            try
            {
                await CheckAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Episode check failed: {ex.Message}");
            }
        } while (await timer.WaitForNextTickAsync().ConfigureAwait(true));
    }

    private async Task CheckAsync()
    {
        if (!AnimeService.IsLoggedIn) return;

        DateTime now = DateTime.Now;
        DateTime lastCheck = _lastCheck.Read(LAST_CHECK_FILE);
        DateTime since = lastCheck > now - MaxLookback ? lastCheck : now - MaxLookback;

        var watching = await animeService.GetUserAnimeListAsync(AnimeStatus.Watching).ConfigureAwait(true);
        var planToWatch = await animeService.GetUserAnimeListAsync(AnimeStatus.PlanToWatch).ConfigureAwait(true);

        var aired = await calendarService.GetAiredAsync(watching.Concat(planToWatch).Select(a => a.Id), since, now).ConfigureAwait(true);
        _lastCheck.Save(LAST_CHECK_FILE, now);

        if (aired.Count == 0) return;
        WeakReferenceMessenger.Default.Send(new NewEpisodesAiredMessage());

        if (saveService.GetSettingsConfig()?.EnableEpisodeNotifications == false) return;

        foreach (AnimeScheduleItem item in aired.OrderBy(i => i.AiringAt))
        {
            await NotifyAsync(item).ConfigureAwait(true);
        }
    }

#if WINDOWS
    private async Task NotifyAsync(AnimeScheduleItem item)
    {
        ToastContentBuilder toast = new ToastContentBuilder()
            .AddArgument("animeId", item.GetId() ?? 0)
            .AddText(item.Title)
            .AddText($"Episode {item.Episode} is out");

        string? poster = await DownloadPosterAsync(item).ConfigureAwait(true);
        if (poster != null) toast.AddAppLogoOverride(new Uri(poster));

        toast.Show();
    }

    private async Task<string?> DownloadPosterAsync(AnimeScheduleItem item)
    {
        try
        {
            string dir = Path.Combine(SaveService.MainDirectory, "ToastImages");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{item.GetId()}.jpg");
            if (!File.Exists(path))
            {
                byte[] bytes = await _client.GetByteArrayAsync(new Uri(item.ImageUrl)).ConfigureAwait(true);
                await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);
            }
            return path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toast poster download failed: {ex.Message}");
            return null;
        }
    }

    private static void OnToastClicked(ToastNotificationActivatedEventArgsCompat e)
    {
        if (!ToastArguments.Parse(e.Argument).TryGetValue("animeId", out string? value)
            || !int.TryParse(value, out int id) || id == 0) return;

        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            {
                window.Show();
                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                window.Activate();
            }

            await DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>().GoToAnime(id).ConfigureAwait(true);
        });
    }
#else
    private static Task NotifyAsync(AnimeScheduleItem item) =>
        ToastService.Show($"New episode: {item.Title} - EP {item.Episode} is out", 5000);
#endif

    public void Dispose() => _client.Dispose();
}

internal sealed class NewEpisodesAiredMessage { }
