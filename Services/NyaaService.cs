using System.Collections.Generic;
using System.Net.Http;
using System.Web;
using System.Threading.Tasks;
using Aniki.Models;
using System.Xml;
using System.Linq;


namespace Aniki.Services
{
    public static class NyaaService
    {
        private static readonly HttpClient _http = new();
        public static async Task<List<NyaaTorrent>> SearchAsync(string animeName, int episodeNumber)
        {
            string term = HttpUtility.UrlEncode($"{animeName} {episodeNumber:D2}");
            string url = $"https://nyaa.si/?page=rss&f=0&c=1_2&q={term}";

            string rssContent = await _http.GetStringAsync(url);
            List<NyaaTorrent> results = new List<NyaaTorrent>();

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(rssContent);

            XmlNodeList? itemNodes = doc.SelectNodes("//item");
            if (itemNodes == null) return results;

            foreach (XmlNode item in itemNodes)
            {
                string? title = item.SelectSingleNode("title")?.InnerText;

                string torrentLink = "";

                XmlNamespaceManager namespaceManager = new XmlNamespaceManager(doc.NameTable);
                namespaceManager.AddNamespace("nyaa", "https://nyaa.si/xmlns/nyaa");
                XmlNode? infoHashNode = item.SelectSingleNode("nyaa:infoHash", namespaceManager);
                
                if (infoHashNode != null)
                {
                    XmlNode? linkNode = item.SelectSingleNode("link");
                    torrentLink = linkNode?.InnerText;
                }

                XmlNode? sizeNode = item.SelectSingleNode("nyaa:size", namespaceManager);
                string size = sizeNode?.InnerText ?? "";

                XmlNode? seedersNode = item.SelectSingleNode("nyaa:seeders", namespaceManager) ??
                                       item.SelectSingleNode("seeders", namespaceManager);
                int seeders = 0;
                if (seedersNode != null)
                {
                    int.TryParse(seedersNode.InnerText, out seeders);
                }

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(torrentLink))
                {
                    results.Add(new()
                    {
                        Title = HttpUtility.HtmlDecode(title),
                        TorrentLink = torrentLink,
                        Size = size,
                        Seeders = seeders
                    });
                }
            }

            results = results.OrderByDescending(x => FuzzySharp.Fuzz.Ratio(x.Title.ToLower(), animeName.ToLower()))
                .ThenByDescending(x => x.Seeders).ToList();

            return results;
        }
    }
}
