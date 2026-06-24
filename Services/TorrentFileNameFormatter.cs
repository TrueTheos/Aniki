using System.Text.RegularExpressions;

namespace Aniki.Services;

public static class TorrentFileNameFormatter
{
    private static readonly Regex ReleaseGroupPattern = new(@"^\[([^\]]+)\]");

    private static (string? ReleaseGroup, IReadOnlyList<TorrentFileNameSegment> Segments) Parse(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return (null, []);

        Match match = ReleaseGroupPattern.Match(fileName);
        if (!match.Success)
        {
            return (null,
            [
                new TorrentFileNameSegment { Text = fileName, Color = KnownSubbers.REST_OF_FILE_NAME_COLOR }
            ]);
        }

        string releaseGroup = match.Groups[1].Value;
        string bracketText = match.Value;
        string remainder = fileName[match.Length..];
        string subberColor = KnownSubbers.GetColor(releaseGroup);

        List<TorrentFileNameSegment> segments = [new() { Text = bracketText, Color = subberColor }];

        if (remainder.Length > 0)
        {
            segments.Add(new TorrentFileNameSegment
            {
                Text = remainder,
                Color = KnownSubbers.REST_OF_FILE_NAME_COLOR
            });
        }

        return (releaseGroup, segments);
    }

    public static void ApplyDisplayMetadata(NyaaTorrent torrent)
    {
        (string? releaseGroup, IReadOnlyList<TorrentFileNameSegment> segments) = Parse(torrent.FileName);
        torrent.ReleaseGroup = releaseGroup;
        torrent.FileNameSegments = segments;
    }
}
