using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CalendarGenerator.Dtos;

namespace CalendarGenerator.Services;

/// <summary>
/// Пример реализации сервиса с тестовыми данными (заглушка).
/// </summary>
public class ExampleService1 : ICalendarService
{
    /// <inheritdoc />
    public string Name => "ExampleService1";

    /// <inheritdoc />
    public Task<CalendarData> GetCalendarDataAsync(int year)
    {
        var nonworkingDays = new List<string> { "0101" };
        var workingDays = new List<string> { "0108" };

        return Task.FromResult(new CalendarData
        {
            Year = year,
            NonworkingDays = nonworkingDays.AsReadOnly(),
            NonworkingDays6 = Array.Empty<string>(),
            WorkingDays = workingDays.AsReadOnly(),
            ShortenedDays = Array.Empty<string>(),
            ShortenedDays6 = Array.Empty<string>()
        });
    }
}
