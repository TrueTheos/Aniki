using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Web;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Aniki.Models;


namespace Aniki.Services
{
    public class NyaaService
    {
        private readonly HttpClient _http = new HttpClient();
        public async Task<List<NyaaTorrent>> SearchAsync(string animeName, int episodeNumber)
        {
            var term = HttpUtility.UrlEncode($"{animeName} {episodeNumber:D2}");
            var url = $"https://nyaa.si/?f=0&c=1_2&q={term}";
            var html = await _http.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var rows = doc.DocumentNode.SelectNodes("//table[contains(@class,'torrent-list')]//tbody//tr");
            var results = new List<NyaaTorrent>();
            if (rows == null) return results;

            foreach (var row in rows)
            {
                var titleNode = row.SelectSingleNode(".//td[2]//a[not(contains(@class,'comments'))]");
                var magnetNode = row.SelectSingleNode(".//td[3]//a[starts-with(@href,'magnet:')]");
                var sizeNode = row.SelectSingleNode(".//td[4]");
                var seedersNode = row.SelectSingleNode(".//td[6]");

                if (titleNode != null && magnetNode != null)
                {
                    results.Add(new NyaaTorrent
                    {
                        Title = HttpUtility.HtmlDecode(titleNode.InnerText.Trim()),
                        MagnetLink = magnetNode.Attributes["href"].Value,
                        Size = sizeNode?.InnerText.Trim() ?? "",
                        Seeders = int.TryParse(seedersNode?.InnerText.Trim(), out var s) ? s : 0
                    });
                }
            }
            return results;
        }
    }
}
