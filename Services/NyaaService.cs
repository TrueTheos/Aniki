using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Web;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Aniki.Models;
using System.Xml;


namespace Aniki.Services
{
    public static class NyaaService
    {
        private static readonly HttpClient _http = new HttpClient();
        public static async Task<List<NyaaTorrent>> SearchAsync(string animeName, int episodeNumber)
        {
            var term = HttpUtility.UrlEncode($"{animeName} {episodeNumber:D2}");
            var url = $"https://nyaa.si/?page=rss&f=0&c=1_2&q={term}";

            var rssContent = await _http.GetStringAsync(url);
            var results = new List<NyaaTorrent>();

            var doc = new XmlDocument();
            doc.LoadXml(rssContent);

            var itemNodes = doc.SelectNodes("//item");
            if (itemNodes == null) return results;

            foreach (XmlNode item in itemNodes)
            {
                var title = item.SelectSingleNode("title")?.InnerText;
                var link = item.SelectSingleNode("link")?.InnerText;

                var descriptionNode = item.SelectSingleNode("description");
                string torrentLink = "";

                var namespaceManager = new XmlNamespaceManager(doc.NameTable);
                namespaceManager.AddNamespace("nyaa", "https://nyaa.si/xmlns/nyaa");
                var infoHashNode = item.SelectSingleNode("nyaa:infoHash", namespaceManager);
                
                if (infoHashNode != null)
                {
                    var infoHash = infoHashNode.InnerText;
                    var encodedTitle = HttpUtility.UrlEncode(title);
                    var linkNode = item.SelectSingleNode("link");
                    torrentLink = linkNode?.InnerText;
                }

                var sizeNode = item.SelectSingleNode("nyaa:size", namespaceManager);
                var size = sizeNode?.InnerText ?? "";

                var seedersNode = item.SelectSingleNode("nyaa:seeders", namespaceManager) ??
                                  item.SelectSingleNode("seeders", namespaceManager);
                int seeders = 0;
                if (seedersNode != null)
                {
                    int.TryParse(seedersNode.InnerText, out seeders);
                }

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(torrentLink))
                {
                    results.Add(new NyaaTorrent
                    {
                        Title = HttpUtility.HtmlDecode(title),
                        TorrentLink = torrentLink,
                        Size = size,
                        Seeders = seeders
                    });
                }
            }

            return results;
        }
    }
}
