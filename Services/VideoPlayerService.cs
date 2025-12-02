using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aniki.Services.Interfaces;
using Avalonia.Threading;

namespace Aniki.Services;

public class VideoPlayerService : IVideoPlayerService
{
    public ObservableCollection<VideoPlayerOption> AvailablePlayers { get; } = new();
    
    public VideoPlayerOption? SelectedPlayer { get; set; }

    private readonly List<string> _videoExtensions = new() { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".m3u8" };
    
    private readonly ConcurrentDictionary<string, VideoPlayerOption> _scannedPlayers = new(StringComparer.OrdinalIgnoreCase);

    public async Task RefreshPlayersAsync()
    {
        await Task.Run(async () =>
        {
            _scannedPlayers.Clear();

            var systemDefault = new VideoPlayerOption
            {
                DisplayName = "System Default",
                ExecutablePath = "",
                IsSystemDefault = true
            };
            _scannedPlayers.TryAdd("", systemDefault);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await DetectWindowsPlayersAsync();
            }
            else
            {
                await DetectUnixPlayersAsync();
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var currentPath = SelectedPlayer?.ExecutablePath;

                AvailablePlayers.Clear();

                var sortedPlayers = _scannedPlayers.Values
                    .OrderByDescending(x => x.IsSystemDefault)
                    .ThenBy(x => x.DisplayName);

                foreach (var player in sortedPlayers)
                {
                    AvailablePlayers.Add(player);
                }

                SelectedPlayer = AvailablePlayers.FirstOrDefault(x => x.ExecutablePath == currentPath) 
                                 ?? AvailablePlayers.FirstOrDefault();
            });
        });
    }

    private async Task DetectWindowsPlayersAsync()
    {
        var tasks = new List<Task>();

        foreach (string ext in _videoExtensions)
        {
            tasks.Add(Task.Run(() => DetectWindowsHandlersForExtension(ext)));
        }

        tasks.Add(Task.Run(() => DetectCommonWindowsPlayers()));

        await Task.WhenAll(tasks);
    }

    private async Task DetectUnixPlayersAsync()
    {
        var commonPlayers = new[] { "mpv", "vlc", "mplayer" };
        
        var tasks = commonPlayers.Select(player => Task.Run(() =>
        {
            if (IsPlayerInstalled(player))
            {
                var option = new VideoPlayerOption
                {
                    DisplayName = player.ToUpper(),
                    ExecutablePath = player
                };
                _scannedPlayers.TryAdd(player, option);
            }
        }));

        await Task.WhenAll(tasks);
    }

    private void DetectCommonWindowsPlayers()
    {
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
                if (File.Exists(path))
                {
                    var option = new VideoPlayerOption
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
            using var extensionKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(extension);
            if (extensionKey != null)
            {
                var progId = extensionKey.GetValue("")?.ToString();
                if (!string.IsNullOrEmpty(progId))
                {
                    using var progIdKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey($"{progId}\\shell\\open\\command");
                    if (progIdKey != null)
                    {
                        var command = progIdKey.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(command))
                        {
                            AddPlayerFromCommand(command, " (Default)");
                        }
                    }
                }
            }

            using var openWithKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @$"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithList");
            
            if (openWithKey != null)
            {
                var valueNames = openWithKey.GetValueNames().Where(n => n != "MRUList");
                foreach (var valueName in valueNames)
                {
                    var appName = openWithKey.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrEmpty(appName))
                    {
                        var exePath = FindExecutableInSystem(appName);
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            var displayName = Path.GetFileNameWithoutExtension(exePath);
                            var option = new VideoPlayerOption
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
        var exePath = ExtractExecutablePath(command);
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            var appName = Path.GetFileNameWithoutExtension(exePath);
            var option = new VideoPlayerOption
            {
                DisplayName = $"{appName}{suffix}",
                ExecutablePath = exePath
            };
            _scannedPlayers.TryAdd(exePath, option);
        }
    }

    private string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrEmpty(command)) return "";
        command = command.Trim();
        
        if (command.StartsWith("\""))
        {
            var endQuote = command.IndexOf("\"", 1);
            if (endQuote > 0) return command.Substring(1, endQuote - 1);
        }
        
        var spaceIndex = command.IndexOf(" ");
        if (spaceIndex > 0) return command.Substring(0, spaceIndex);
        
        return command;
    }

    private string? FindExecutableInSystem(string exeName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar != null)
        {
            foreach (var path in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    var fullPath = Path.Combine(path, exeName);
                    if (File.Exists(fullPath)) return fullPath;
                }
                catch { /* Ignore invalid paths */ }
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
                if (!Directory.Exists(baseDir)) continue;

                var file = Directory.EnumerateFiles(baseDir, exeName, new EnumerationOptions
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
                    process.WaitForExit(1000);
                    return process.ExitCode == 0;
                }
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
            {
                return OpenWithSystemDefault(url);
            }
            else
            {
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
                var tempFile = Path.Combine(Path.GetTempPath(), $"aniki_temp_{Guid.NewGuid()}.m3u8");
                File.WriteAllText(tempFile, url);

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tempFile,
                    UseShellExecute = true
                });

                Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                });

                return process;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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

    private Process? OpenWithSpecificPlayer(string url, string playerPath)
    {
        try
        {
            var playerName = Path.GetFileNameWithoutExtension(playerPath).ToLower();
            
            string arguments = playerName switch
            {
                "mpv" or "mpvnet" => $"\"{url}\" --force-window=yes --title=\"Aniki Player\"",
                "vlc" => $"\"{url}\" --meta-title=\"Aniki Player\"",
                var n when n.Contains("mpc") => $"\"{url}\"", 
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