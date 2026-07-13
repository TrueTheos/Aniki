namespace Aniki.Services.Interfaces;

internal interface ICalendarService
{
    public Task<List<DaySchedule>> GetScheduleAsync(
        IEnumerable<int> watchingList,
        DateTime startDate,
        DateTime endDate,
        int perPage = 150);

    public Task<List<AnimeScheduleItem>> GetAnimeScheduleForDayAsync(DateTime date);
}