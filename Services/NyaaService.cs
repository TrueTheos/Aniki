using System.Web;
using System.Xml;
using Aniki.Services.Interfaces;


namespace Aniki.Services;

internal sealed class NyaaService : INyaaService, IDisposable
{
    private readonly HttpClient _http = new();

   public async Task<List<NyaaTorrent>> SearchAsync(string animeName, string torrentSearchTerms)
    {
        string url = string.IsNullOrWhiteSpace(torrentSearchTerms) ? $"https://nyaa.si/?page=rss&f=0&c=0_0&q={HttpUtility.UrlEncode($"{animeName}")}" : $"https://nyaa.si/?page=rss&f=0&c=0_0&q={HttpUtility.UrlEncode($"{torrentSearchTerms}")}";

        string rssContent = await _http.GetStringAsync(url).ConfigureAwait(false);
        List<NyaaTorrent> results = new();

        XmlDocument doc = new();
        doc.LoadXml(rssContent);

        XmlNodeList? itemNodes = doc.SelectNodes("//item");
        if (itemNodes == null) return results;

        foreach (XmlNode item in itemNodes)
        {
            string? title = item.SelectSingleNode("title")?.InnerText;
            if(title == null) continue;

            XmlNamespaceManager namespaceManager = new(doc.NameTable);
            namespaceManager.AddNamespace("nyaa", "https://nyaa.si/xmlns/nyaa");
            XmlNode? infoHashNode = item.SelectSingleNode("nyaa:infoHash", namespaceManager);
                
            if (infoHashNode == null) continue;
            XmlNode? linkNode = item.SelectSingleNode("link");
                
            if(linkNode == null) continue;
            string torrentLink = linkNode.InnerText;

            XmlNode? sizeNode = item.SelectSingleNode("nyaa:size", namespaceManager);
            string size = sizeNode?.InnerText ?? "";

            XmlNode? seedersNode = item.SelectSingleNode("nyaa:seeders", namespaceManager) ??
                                   item.SelectSingleNode("seeders", namespaceManager);
            
            int seeders = seedersNode != null && int.TryParse(seedersNode.InnerText, out int parsedSeeders)
                ? parsedSeeders
                : 0;

            XmlNode? pubDateNode = item.SelectSingleNode("pubDate");
            
            DateTime publishDate = pubDateNode != null && DateTime.TryParse(pubDateNode.InnerText, out DateTime parsedDate)
                ? parsedDate
                : DateTime.MinValue;

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(torrentLink))
            {
                results.Add(new NyaaTorrent
                {
                    FileName = HttpUtility.HtmlDecode(title),
                    TorrentLink = torrentLink,
                    Size = size,
                    Seeders = seeders,
                    PublishDate = publishDate
                });
            }
        }

        return results;
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}