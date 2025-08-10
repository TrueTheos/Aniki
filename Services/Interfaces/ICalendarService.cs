namespace Aniki.Services.Interfaces;

public interface ICalendarService
{
    public Task<List<DaySchedule>> GetScheduleAsync(
        IEnumerable<string> watchingList,
        DateTime startDate,
        DateTime endDate,
        int perPage = 150);

    public Task<List<AnimeScheduleItem>> GetAnimeScheduleForDayAsync(DateTime date,
        IEnumerable<string> watchingList);
}