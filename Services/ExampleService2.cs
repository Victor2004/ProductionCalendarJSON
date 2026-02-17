using System;
using System.Threading.Tasks;
using CalendarGenerator.Dtos;

namespace CalendarGenerator.Services;

/// <summary>
/// Вторая примерная реализация сервиса (пустая заглушка).
/// </summary>
public class ExampleService2 : ICalendarService
{
    /// <inheritdoc />
    public string Name => "ExampleService2";

    /// <inheritdoc />
    public Task<CalendarData> GetCalendarDataAsync(int year)
    {
        return Task.FromResult(new CalendarData
        {
            Year = year,
            NonworkingDays = Array.Empty<string>(),
            NonworkingDays6 = Array.Empty<string>(),
            WorkingDays = Array.Empty<string>(),
            ShortenedDays = Array.Empty<string>(),
            ShortenedDays6 = Array.Empty<string>()
        });
    }
}
