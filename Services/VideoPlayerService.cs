using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aniki.Services.Interfaces;
using Avalonia.Threading;
using Microsoft.Win32;

namespace Aniki.Services;

internal sealed class VideoPlayerService : IVideoPlayerService
{
    private const string ALLANIME_STREAM_REFERER = "https://allmanga.to/";
    private const string MP4UPLOAD_STREAM_REFERER = "https://www.mp4upload.com/";

    private const string ALLANIME_BROWSER_USER_AGENT =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36";

    public ObservableCollection<VideoPlayerOption> AvailablePlayers { get; } = [];
    
    public VideoPlayerOption? SelectedPlayer { get; set; }

    private readonly IReadOnlyList<string> _videoExtensions = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".m3u8"];
    
    private readonly ConcurrentDictionary<string, VideoPlayerOption> _scannedPlayers = new(StringComparer.OrdinalIgnoreCase);

    private readonly string? _preferredPlayerPath;

    public VideoPlayerService(ISaveService saveService)
    {
        SettingsConfig? config = saveService.GetSettingsConfig();
        _preferredPlayerPath = config?.PreferredVideoPlayerPath;
    }

    public async Task ScanPlayersAsync()
    {
        await Task.Run(async () =>
        {
            _scannedPlayers.Clear();

            VideoPlayerOption systemDefault = new()
            {
                DisplayName = "System Default",
                ExecutablePath = "",
                IsSystemDefault = true
            };
            _scannedPlayers.TryAdd("", systemDefault);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await DetectWindowsPlayersAsync().ConfigureAwait(true);
            }
            else
            {
                await DetectUnixPlayersAsync().ConfigureAwait(true);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                string? currentPath = SelectedPlayer?.ExecutablePath ?? _preferredPlayerPath;

                AvailablePlayers.Clear();

                var sortedPlayers = _scannedPlayers.Values
                                                   .OrderByDescending(x => x.IsSystemDefault)
                                                   .ThenBy(x => x.DisplayName);

                foreach (VideoPlayerOption player in sortedPlayers)
                {
                    AvailablePlayers.Add(player);
                }

                SelectedPlayer = AvailablePlayers.FirstOrDefault(x => x.ExecutablePath == currentPath) 
                                 ?? AvailablePlayers.FirstOrDefault();
            });
        }).ConfigureAwait(true);
    }

    private async Task DetectWindowsPlayersAsync()
    {
        List<Task> tasks = [];
        tasks.AddRange(_videoExtensions.Select(ext => Task.Run(() => DetectWindowsHandlersForExtension(ext))));

        tasks.Add(Task.Run(DetectCommonWindowsPlayers));

        await Task.WhenAll(tasks).ConfigureAwait(true);
    }

    private async Task DetectUnixPlayersAsync()
    {
        string[] commonPlayers = ["mpv", "vlc", "mplayer"];
        
        var tasks = commonPlayers.Select(player => Task.Run(() =>
        {
            if (IsPlayerInstalled(player))
            {
                VideoPlayerOption option = new()
                {
                    DisplayName = player.ToUpperInvariant(),
                    ExecutablePath = player
                };
                _scannedPlayers.TryAdd(player, option);
            }
        }));

        await Task.WhenAll(tasks).ConfigureAwait(true);
    }

    private void DetectCommonWindowsPlayers()
    {
        Dictionary<string, string[]> commonPaths = new()
        {
            ["VLC"] =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC", "vlc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VideoLAN", "VLC", "vlc.exe")
            ],
            ["MPV"] =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "mpv", "mpv.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mpv", "mpv.exe")
            ],
            ["MPC-HC"] =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MPC-HC", "mpc-hc64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MPC-HC", "mpc-hc.exe")
            ],
            ["MPC-BE"] =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MPC-BE", "mpc-be64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MPC-BE", "mpc-be.exe")
            ]
        };

        foreach ((string name, string[] paths) in commonPaths)
        {
            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    VideoPlayerOption option = new()
                    {
                        DisplayName = name,
                        ExecutablePath = path
                    };

                    _scannedPlayers.TryAdd(path, option);
                    break;
                }
            }
        }
    }

    private void DetectWindowsHandlersForExtension(string extension)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using RegistryKey? extensionKey = Registry.ClassesRoot.OpenSubKey(extension);
            if (extensionKey != null)
            {
                string? progId = extensionKey.GetValue("")?.ToString();
                if (!string.IsNullOrEmpty(progId))
                {
                    using RegistryKey? progIdKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
                    if (progIdKey != null)
                    {
                        string? command = progIdKey.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(command))
                        {
                            AddPlayerFromCommand(command, " (Default)");
                        }
                    }
                }
            }

            using RegistryKey? openWithKey = Registry.CurrentUser.OpenSubKey(
                @$"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithList");
            
            if (openWithKey != null)
            {
                var valueNames = openWithKey.GetValueNames().Where(n => n != "MRUList");
                foreach (string valueName in valueNames)
                {
                    string? appName = openWithKey.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrEmpty(appName))
                    {
                        string? exePath = FindExecutableInSystem(appName);
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            string displayName = Path.GetFileNameWithoutExtension(exePath);
                            VideoPlayerOption option = new()
                            {
                                DisplayName = displayName,
                                ExecutablePath = exePath
                            };
                            _scannedPlayers.TryAdd(exePath, option);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking extension {extension}: {ex.Message}");
        }
    }

    private void AddPlayerFromCommand(string command, string suffix = "")
    {
        string exePath = ExtractExecutablePath(command);
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            string appName = Path.GetFileNameWithoutExtension(exePath);
            VideoPlayerOption option = new()
            {
                DisplayName = $"{appName}{suffix}",
                ExecutablePath = exePath
            };
            _scannedPlayers.TryAdd(exePath, option);
        }
    }

    private static string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrEmpty(command)) return "";
        command = command.Trim();
        
        if (command.StartsWith('"'))
        {
            int endQuote = command.IndexOf('"', 1);
            if (endQuote > 0) return command[1..endQuote];
        }
        
        int spaceIndex = command.IndexOf(' ', StringComparison.InvariantCulture);
        return spaceIndex > 0 ? command[..spaceIndex] : command;
    }

    private static string? FindExecutableInSystem(string exeName)
    {
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar != null)
        {
            foreach (string path in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    string fullPath = Path.Combine(path, exeName);
                    if (File.Exists(fullPath)) return fullPath;
                }
                catch { /* Ignore invalid paths */ }
            }
        }

        string[] programFiles =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        ];

        foreach (string baseDir in programFiles)
        {
            try
            {
                if (!Directory.Exists(baseDir)) continue;

                string? file = Directory.EnumerateFiles(baseDir, exeName, new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 2,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false
                }).FirstOrDefault();

                if (file != null) return file;
            }
            catch { /* Ignore access denied */ }
        }

        return null;
    }

    private static bool IsPlayerInstalled(string playerExe)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return FindExecutableInSystem(playerExe + ".exe") != null;
            }

            ProcessStartInfo startInfo = new()
            {
                FileName               = "which",
                Arguments              = playerExe,
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using Process? process = Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit(1000);
                return process.ExitCode == 0;
            }
        }
        catch { /* Detection failed */ }
        return false;
    }

    public Process? OpenVideo(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        try
        {
            if (SelectedPlayer == null || SelectedPlayer.IsSystemDefault)
                return OpenWithSystemDefault(url);

            return OpenWithSpecificPlayer(url, SelectedPlayer.ExecutablePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open video: {ex}");
            throw;
        }
    }

    private static bool IsRemoteStreamUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return false;

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalMediaPath(string url) =>
        File.Exists(url) || Directory.Exists(url);

    private VideoPlayerOption? FindStreamCapablePlayer()
    {
        string[] preferredNames = ["mpv", "mpvnet", "vlc", "mpc-hc64", "mpc-hc", "mpc-be64", "mpc-be"];

        foreach (string name in preferredNames)
        {
            VideoPlayerOption? player = AvailablePlayers.FirstOrDefault(p =>
                !p.IsSystemDefault &&
                Path.GetFileNameWithoutExtension(p.ExecutablePath)
                    .Equals(name, StringComparison.OrdinalIgnoreCase));

            if (player != null)
                return player;
        }

        return AvailablePlayers.FirstOrDefault(p => !p.IsSystemDefault);
    }

    private Process? OpenWithSystemDefault(string url)
    {
        try
        {
            if (IsRemoteStreamUrl(url))
            {
                VideoPlayerOption? player = FindStreamCapablePlayer();
                if (player != null)
                    return OpenWithSpecificPlayer(url, player.ExecutablePath);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Process.Start(new ProcessStartInfo
                {
                    FileName        = url,
                    UseShellExecute = true
                });
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Process.Start(new ProcessStartInfo("open", url) { UseShellExecute = false });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open with system default: {ex}");
        }

        return null;
    }

    private static string GetStreamReferer(string url)
    {
        return url.Contains("mp4upload.com", StringComparison.OrdinalIgnoreCase) ? MP4UPLOAD_STREAM_REFERER : ALLANIME_STREAM_REFERER;
    }

    private static Process? OpenWithSpecificPlayer(string url, string playerPath)
    {
        try
        {
            string playerName = Path.GetFileNameWithoutExtension(playerPath).ToLowerInvariant();
            bool useStreamHeaders = IsRemoteStreamUrl(url) && !IsLocalMediaPath(url);
            string streamReferer = GetStreamReferer(url);
            string arguments = playerName switch
            {
                "mpv" or "mpvnet" => useStreamHeaders
                    ? $"\"{url}\" --user-agent=\"{ALLANIME_BROWSER_USER_AGENT}\" " +
                      $"--http-header-fields=\"Referer: {streamReferer}; Origin: https://allmanga.to\" " +
                      $"--force-window=yes --title=\"Aniki Player\""
                    : $"\"{url}\" --force-window=yes --title=\"Aniki Player\"",
                "vlc" => useStreamHeaders
                    ? $"--http-referrer=\"{streamReferer}\" --http-user-agent=\"{ALLANIME_BROWSER_USER_AGENT}\" " +
                      $"\"{url}\" --meta-title=\"Aniki Player\""
                    : $"\"{url}\" --meta-title=\"Aniki Player\"",
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
}