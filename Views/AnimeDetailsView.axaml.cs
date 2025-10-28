using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class AnimeDetailsView : UserControl
{
    public AnimeDetailsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
    
    private void PlayTrailerVideo(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string trailerUrl)
        {
            PlayTrailer(trailerUrl);
        }
    }

    private void PlayTrailer(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ConvertEmbedToWatchUrl(url),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Information($"Error opening video: {ex.Message}");
        }
    }
    
    static string ConvertEmbedToWatchUrl(string embedUrl)
    {
        var match = Regex.Match(embedUrl, @"embed/([a-zA-Z0-9_-]+)");
        if (match.Success)
        {
            string videoId = match.Groups[1].Value;
            return $"https://www.youtube.com/watch?v={videoId}";
        }
        else
        {
            return embedUrl;
        }
    }
}