using System.Web;
using System.Xml;
using Aniki.Services.Interfaces;


namespace Aniki.Services;

public class NyaaService : INyaaService
{
    private readonly HttpClient _http = new();

   public async Task<List<NyaaTorrent>> SearchAsync(string animeName, string torrentSearchTerms)
    {
        string term = HttpUtility.UrlEncode($"{animeName}");
        string searchTerm = HttpUtility.UrlEncode($"{torrentSearchTerms}");
        string url = $"https://nyaa.si/?page=rss&f=0&c=1_2&q={term} {searchTerm}";

        string rssContent = await _http.GetStringAsync(url);
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
            int seeders = 0;
            if (seedersNode != null)
            {
                int.TryParse(seedersNode.InnerText, out seeders);
            }

            XmlNode? pubDateNode = item.SelectSingleNode("pubDate");
            DateTime publishDate = DateTime.MinValue;
            if (pubDateNode != null)
            {
                DateTime.TryParse(pubDateNode.InnerText, out publishDate);
            }

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(torrentLink))
            {
                results.Add(new()
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
}