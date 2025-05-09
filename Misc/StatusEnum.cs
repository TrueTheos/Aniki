using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Aniki.Services.SaveService;

namespace Aniki.Misc
{
    public enum AnimeStatusAPI { watching, completed, on_hold, dropped, plan_to_watch, all }
    public enum AnimeStatusTranslated { Watching, Completed, OnHold, Dropped, PlanToWatch, All } //todo to wyjebac i uzywac tylko API enumow

    public static class StatusEnum
    {
        public static IReadOnlyList<AnimeStatusTranslated> TranslatedStatusOptions { get; } = [.. Enum.GetValues<AnimeStatusTranslated>().Cast<AnimeStatusTranslated>()];
        public static IReadOnlyList<AnimeStatusAPI> APIStatusOptions { get; } = [.. Enum.GetValues<AnimeStatusAPI>().Cast<AnimeStatusAPI>()];

        public static AnimeStatusTranslated StringToTranslated(string text)
        {
            return text switch
            {
                "Watching" => AnimeStatusTranslated.Watching,
                "Completed" => AnimeStatusTranslated.Completed,
                "On Hold" => AnimeStatusTranslated.OnHold,
                "Dropped" => AnimeStatusTranslated.Dropped,
                "Plan to Watch" => AnimeStatusTranslated.PlanToWatch,
                _ => throw new ArgumentOutOfRangeException(nameof(text), text, null)
            };
        }

        public static AnimeStatusAPI StringToAPI(string text)
        {
            return text switch
            {
                "watching" => AnimeStatusAPI.watching,
                "completed" => AnimeStatusAPI.completed,
                "on_hold" => AnimeStatusAPI.on_hold,
                "dropped" => AnimeStatusAPI.dropped,
                "plan_to_watch" => AnimeStatusAPI.plan_to_watch,
                _ => throw new ArgumentOutOfRangeException(nameof(text), text, null)
            };
        }

        public static AnimeStatusAPI TranslatedToAPI(this AnimeStatusTranslated translated)
        {
            return translated switch
            {
                AnimeStatusTranslated.Watching => AnimeStatusAPI.watching,
                AnimeStatusTranslated.Completed => AnimeStatusAPI.completed,
                AnimeStatusTranslated.OnHold => AnimeStatusAPI.on_hold,
                AnimeStatusTranslated.Dropped => AnimeStatusAPI.dropped,
                AnimeStatusTranslated.PlanToWatch => AnimeStatusAPI.plan_to_watch,
                AnimeStatusTranslated.All => AnimeStatusAPI.all,
                _ => throw new ArgumentOutOfRangeException(nameof(translated), translated, null)
            };
        }

        public static AnimeStatusTranslated APIToTranslated(this AnimeStatusAPI api)
        {
            return api switch
            {
                AnimeStatusAPI.watching => AnimeStatusTranslated.Watching,
                AnimeStatusAPI.completed => AnimeStatusTranslated.Completed,
                AnimeStatusAPI.on_hold => AnimeStatusTranslated.OnHold,
                AnimeStatusAPI.dropped => AnimeStatusTranslated.Dropped,
                AnimeStatusAPI.plan_to_watch => AnimeStatusTranslated.PlanToWatch,
                AnimeStatusAPI.all => AnimeStatusTranslated.All,
                _ => throw new ArgumentOutOfRangeException(nameof(api), api, null)
            };
        }
    }
}
