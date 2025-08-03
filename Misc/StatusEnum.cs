using System;
using System.Collections.Generic;

namespace Aniki.Misc
{
    public enum AnimeStatusApi { watching, completed, on_hold, dropped, plan_to_watch, none }
    public enum AnimeStatusTranslated { Watching, Completed, OnHold, Dropped, PlanToWatch, None } //todo to wyjebac i uzywac tylko API enumow

    public static class StatusEnum
    {
        public static AnimeStatusTranslated StringToTranslated(string text)
        {
            return text switch
            {
                "Watching" => AnimeStatusTranslated.Watching,
                "Completed" => AnimeStatusTranslated.Completed,
                "On Hold" => AnimeStatusTranslated.OnHold,
                "Dropped" => AnimeStatusTranslated.Dropped,
                "Plan to Watch" => AnimeStatusTranslated.PlanToWatch,
            };
        }

        public static AnimeStatusApi StringToApi(string text)
        {
            return text switch
            {
                "watching" => AnimeStatusApi.watching,
                "completed" => AnimeStatusApi.completed,
                "on_hold" => AnimeStatusApi.on_hold,
                "dropped" => AnimeStatusApi.dropped,
                "plan_to_watch" => AnimeStatusApi.plan_to_watch,
                "none" => AnimeStatusApi.none
            };
        }

        public static AnimeStatusApi TranslatedToApi(this AnimeStatusTranslated translated)
        {
            return translated switch
            {
                AnimeStatusTranslated.Watching => AnimeStatusApi.watching,
                AnimeStatusTranslated.Completed => AnimeStatusApi.completed,
                AnimeStatusTranslated.OnHold => AnimeStatusApi.on_hold,
                AnimeStatusTranslated.Dropped => AnimeStatusApi.dropped,
                AnimeStatusTranslated.PlanToWatch => AnimeStatusApi.plan_to_watch,
                AnimeStatusTranslated.None => AnimeStatusApi.none,
                _ => throw new ArgumentOutOfRangeException(nameof(translated), translated, null)
            };
        }

        public static AnimeStatusTranslated ApiToTranslated(this AnimeStatusApi api)
        {
            return api switch
            {
                AnimeStatusApi.watching => AnimeStatusTranslated.Watching,
                AnimeStatusApi.completed => AnimeStatusTranslated.Completed,
                AnimeStatusApi.on_hold => AnimeStatusTranslated.OnHold,
                AnimeStatusApi.dropped => AnimeStatusTranslated.Dropped,
                AnimeStatusApi.plan_to_watch => AnimeStatusTranslated.PlanToWatch,
                AnimeStatusApi.none => AnimeStatusTranslated.None,
                _ => throw new ArgumentOutOfRangeException(nameof(api), api, null)
            };
        }
    }
}
