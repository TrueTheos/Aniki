using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aniki.Services.Interfaces;
using Aniki.Views;
using Avalonia.Controls.ApplicationLifetimes;

namespace Aniki.ViewModels;

public partial class OnlineViewModel : ViewModelBase, IDisposable
{
    private readonly IAllMangaScraperService _scraperService;
    private readonly IMalService _malService;
    private string? _currentVideoUrl;
    private Process? _videoProcess;
    
    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _statusText = "Search for anime to get started";

    [ObservableProperty]
    private ObservableCollection<AllMangaSearchResult> _animeResults = new();

    private AllMangaSearchResult? _selectedAnime;
    public AllMangaSearchResult? SelectedAnime
    {
        get => _selectedAnime;
        set
        {
            if (SetProperty(ref _selectedAnime, value))
            {
                OnSelectedAnimeChanged(value);
            }
        }
    }

    [ObservableProperty]
    private ObservableCollection<AllManagaEpisode> _episodes = new();

    private AllManagaEpisode? _selectedEpisode;
    
    public AllManagaEpisode? SelectedEpisode
    {
        get => _selectedEpisode;
        set
        {
            if (SetProperty(ref _selectedEpisode, value))
            {
                OnSelectedEpisodeChanged(value);
            }
        }
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _canPlayVideo;

    [ObservableProperty]
    private string _watchedEpisodesText = "No episodes watched yet";

    [ObservableProperty]
    private VideoPlayerOption? _selectedPlayer;

    public ObservableCollection<VideoPlayerOption> AvailablePlayers { get; } = new();

    private int? _watchingMalId;

    public class VideoPlayerOption
    {
        public string DisplayName { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public bool IsSystemDefault { get; set; }

        public override string ToString() => DisplayName;
    }
    
    public OnlineViewModel(IAllMangaScraperService scraperService, IMalService malService)
    {
        _watchingMalId = null;
        _scraperService = scraperService;
        _malService = malService;
        DetectAvailablePlayers();
        
        SelectedPlayer = AvailablePlayers.FirstOrDefault();
    }

    private void DetectAvailablePlayers()
    {
        AvailablePlayers.Add(new VideoPlayerOption
        {
            DisplayName = "System Default",
            ExecutablePath = "",
            IsSystemDefault = true
        });

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            DetectWindowsM3U8Handlers();
        }
        else
        {
            var commonPlayers = new[] { "mpv", "vlc", "mplayer" };
            foreach (var player in commonPlayers)
            {
                if (IsPlayerInstalled(player))
                {
                    AvailablePlayers.Add(new VideoPlayerOption
                    {
                        DisplayName = player.ToUpper(),
                        ExecutablePath = player
                    });
                }
            }
        }
    }

    private void DetectWindowsM3U8Handlers()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var m3u8Key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(".m3u8");
                if (m3u8Key != null)
                {
                    var progId = m3u8Key.GetValue("")?.ToString();
                    if (!string.IsNullOrEmpty(progId))
                    {
                        using var progIdKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey($"{progId}\\shell\\open\\command");
                        if (progIdKey != null)
                        {
                            var command = progIdKey.GetValue("")?.ToString();
                            if (!string.IsNullOrEmpty(command))
                            {
                                var exePath = ExtractExecutablePath(command);
                                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                                {
                                    var appName = Path.GetFileNameWithoutExtension(exePath);
                                    AvailablePlayers.Add(new VideoPlayerOption
                                    {
                                        DisplayName = $"{appName} (Default)",
                                        ExecutablePath = exePath
                                    });
                                }
                            }
                        }
                    }
                }

                using var openWithKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.m3u8\OpenWithList");
                
                if (openWithKey != null)
                {
                    var apps = new HashSet<string>();
                    foreach (var valueName in openWithKey.GetValueNames())
                    {
                        if (valueName == "MRUList") continue;
                        
                        var appName = openWithKey.GetValue(valueName)?.ToString();
                        if (!string.IsNullOrEmpty(appName) && apps.Add(appName))
                        {
                            var exePath = FindExecutableInSystem(appName);
                            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                            {
                                var displayName = Path.GetFileNameWithoutExtension(exePath);
                                
                                if (!AvailablePlayers.Any(p => p.ExecutablePath.Equals(exePath, StringComparison.OrdinalIgnoreCase)))
                                {
                                    AvailablePlayers.Add(new VideoPlayerOption
                                    {
                                        DisplayName = displayName,
                                        ExecutablePath = exePath
                                    });
                                }
                            }
                        }
                    }
                }

                var commonPaths = new Dictionary<string, string[]>
                {
                    ["VLC"] = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC", "vlc.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VideoLAN", "VLC", "vlc.exe")
                    },
                    ["MPV"] = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "mpv", "mpv.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mpv", "mpv.exe")
                    },
                    ["MPC-HC"] = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MPC-HC", "mpc-hc64.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MPC-HC", "mpc-hc.exe")
                    },
                    ["MPC-BE"] = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MPC-BE", "mpc-be64.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MPC-BE", "mpc-be.exe")
                    }
                };

                foreach (var (name, paths) in commonPaths)
                {
                    foreach (var path in paths)
                    {
                        if (File.Exists(path) && !AvailablePlayers.Any(p => p.ExecutablePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                        {
                            AvailablePlayers.Add(new VideoPlayerOption
                            {
                                DisplayName = name,
                                ExecutablePath = path
                            });
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting video players: {ex}");
            }
        }
    }

    private string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrEmpty(command))
            return "";

        command = command.Trim();
        
        if (command.StartsWith("\""))
        {
            var endQuote = command.IndexOf("\"", 1);
            if (endQuote > 0)
            {
                return command.Substring(1, endQuote - 1);
            }
        }
        
        var spaceIndex = command.IndexOf(" ");
        if (spaceIndex > 0)
        {
            return command.Substring(0, spaceIndex);
        }
        
        return command;
    }

    private string? FindExecutableInSystem(string exeName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar != null)
        {
            foreach (var path in pathVar.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, exeName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        var programFiles = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (var baseDir in programFiles)
        {
            try
            {
                var files = Directory.GetFiles(baseDir, exeName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            catch
            {
                // Ignore access denied errors
            }
        }

        return null;
    }

    private bool IsPlayerInstalled(string playerExe)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return FindExecutableInSystem(playerExe + ".exe") != null;
            }
            else
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = playerExe,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
        }
        catch
        {
            // If detection fails assume not installed
        }

        return false;
    }

    [RelayCommand]
    private async Task SearchAnimeAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        IsLoading = true;
        StatusText = "Searching anime...";
        AnimeResults.Clear();
        Episodes.Clear();
        SelectedAnime = null;
        CanPlayVideo = false;

        try
        {
            var results = await _scraperService.SearchAnimeAsync(SearchQuery);
            
            foreach (var result in results)
            {
                AnimeResults.Add(result);
            }

            StatusText = results.Count > 0 ? $"Found {results.Count} anime" : "No results found";
        }
        catch (Exception ex)
        {
            StatusText = $"Search error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSelectedAnimeChanged(AllMangaSearchResult? value)
    {
        if (value != null)
        {
            _ = LoadEpisodesAsync(value);
            _ = GetEpisodesWatched(value.MalId!.Value);
        }
        else
        {
            Episodes.Clear();
            CanPlayVideo = false;
        }
    }

    private async Task LoadEpisodesAsync(AllMangaSearchResult anime)
    {
        IsLoading = true;
        StatusText = "Loading episodes...";
        Episodes.Clear();
        CanPlayVideo = false;

        try
        {
            var episodes = await _scraperService.GetEpisodesAsync(anime.Url);
            
            foreach (var episode in episodes)
            {
                Episodes.Add(episode);
            }

            StatusText = episodes.Count > 0
                ? $"Loaded {episodes.Count} episodes - Select one to watch"
                : "No episodes available";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading episodes: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void OnSelectedEpisodeChanged(AllManagaEpisode? value)
    {
        CanPlayVideo = false;
        _currentVideoUrl = null;

        if (value != null)
        {
            await PrepareVideoAsync(value);
        }
    }

    private async Task PrepareVideoAsync(AllManagaEpisode episode)
    {
        IsLoading = true;
        StatusText = "Preparing video...";

        try
        {
            _currentVideoUrl = await _scraperService.GetVideoUrlAsync(episode.Url!);
            CanPlayVideo = !string.IsNullOrEmpty(_currentVideoUrl);
            StatusText = $"Episode {episode.Number} ready - Click 'Play' to watch";
        }
        catch (Exception ex)
        {
            StatusText = $"Error preparing video: {ex.Message}";
            CanPlayVideo = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PlayInBrowser()
    {
        if (string.IsNullOrEmpty(_currentVideoUrl) || SelectedEpisode == null)
        {
            StatusText = "No video URL available";
            return;
        }

        try
        {
            if (_videoProcess != null && !_videoProcess.HasExited)
            {
                _videoProcess.Exited -= OnVideoProcessExited;
            }

            _videoProcess = OpenVideoWithPlayer(_currentVideoUrl);
            
            if (_videoProcess != null)
            {
                if(SelectedAnime != null) _watchingMalId = SelectedAnime.MalId;
                _videoProcess.EnableRaisingEvents = true;
                _videoProcess.Exited += OnVideoProcessExited;
                
                var playerName = SelectedPlayer?.DisplayName ?? "video player";
                StatusText = $"Playing Episode {SelectedEpisode.Number} in {playerName}";
            }
            else
            {
                StatusText = "Video player opened";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening video player: {ex.Message}";
            Debug.WriteLine(ex);
        }
    }

    private void OnVideoProcessExited(object? sender, EventArgs e)
    {
        if (SelectedEpisode != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    ConfirmEpisodeWindow dialog = new() 
                    {
                        DataContext = new ConfirmEpisodeViewModel(SelectedEpisode.Number)
                    };

                    bool result = await dialog.ShowDialog<bool>(desktop.MainWindow!);

                    if (result)
                    {
                        if (SelectedEpisode != null)
                        {
                            _ = _malService.UpdateEpisodesWatched(_watchingMalId!.Value, SelectedEpisode.Number);
                            UpdateWatchedEpisodesText(SelectedEpisode.Number);
                        }
                    }
                }
            });
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"Finished watching Episode {SelectedEpisode.Number}";
            });
        }

        _videoProcess?.Dispose();
        _videoProcess = null;
    }

    private async Task GetEpisodesWatched(int malId)
    {
        var animeFieldSet = await _malService.GetFieldsAsync(malId, AnimeField.MY_LIST_STATUS);

        if (animeFieldSet.MyListStatus == null)
        {
            UpdateWatchedEpisodesText(0);
        }
        else
        {
            int episodesWatched = animeFieldSet.MyListStatus.NumEpisodesWatched;
            UpdateWatchedEpisodesText(episodesWatched);
        }
    }

    private void UpdateWatchedEpisodesText(int episodes)
    {
        WatchedEpisodesText = episodes switch
        {
            0 => "No episodes watched yet",
            1 => "1 episode watched",
            _ => $"{episodes} episodes watched"
        };
    }

    private Process? OpenVideoWithPlayer(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            if (SelectedPlayer == null || SelectedPlayer.IsSystemDefault)
            {
                // Use Windows "Open With" / System Default
                return OpenWithSystemDefault(url);
            }
            else
            {
                // Use specific player
                return OpenWithSpecificPlayer(url, SelectedPlayer.ExecutablePath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open video: {ex}");
            throw;
        }
    }

    private Process? OpenWithSystemDefault(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Create a temporary .m3u8 file to trigger the file association
                var tempFile = Path.Combine(Path.GetTempPath(), $"aniki_temp_{Guid.NewGuid()}.m3u8");
                File.WriteAllText(tempFile, url);

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tempFile,
                    UseShellExecute = true
                });

                // Clean up temp file after a delay
                Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    try { File.Delete(tempFile); } catch { }
                });

                return process;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Process.Start(new ProcessStartInfo("xdg-open", url)
                {
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Process.Start(new ProcessStartInfo("open", url)
                {
                    UseShellExecute = false
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open with system default: {ex}");
        }

        return null;
    }

    private Process? OpenWithSpecificPlayer(string url, string playerPath)
    {
        try
        {
            var playerName = Path.GetFileNameWithoutExtension(playerPath).ToLower();
            
            string arguments = playerName switch
            {
                "mpv" or "mpvnet" => $"\"{url}\" --force-window=yes --title=\"Aniki Player\"",
                "vlc" => $"\"{url}\" --meta-title=\"Aniki Player\"",
                "mpc-hc64" or "mpc-hc" or "mpc-be64" or "mpc-be" => $"\"{url}\"",
                _ => $"\"{url}\""
            };

            return Process.Start(new ProcessStartInfo
            {
                FileName = playerPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open with specific player: {ex}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_videoProcess != null)
        {
            _videoProcess.Exited -= OnVideoProcessExited;
            _videoProcess.Dispose();
        }
        
        _currentVideoUrl = null;
    }
}