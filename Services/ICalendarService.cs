using System.Threading.Tasks;
using CalendarGenerator.Dtos;

namespace CalendarGenerator.Services;

/// <summary>
/// Интерфейс сервиса получения данных производственного календаря.
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Название сервиса.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Получает данные календаря для указанного года.
    /// </summary>
    /// <param name="year">Год.</param>
    /// <returns>Объект <see cref="CalendarData"/> с данными за год.</returns>
    Task<CalendarData> GetCalendarDataAsync(int year);
}
