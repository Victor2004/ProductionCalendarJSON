using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CalendarGenerator.Dtos;

namespace CalendarGenerator.Services;

/// <summary>
/// Реализация сервиса календаря на основе API isdayoff.ru.
/// </summary>
public class IsDayOffService : ICalendarService
{
    /// <inheritdoc />
    public string Name => "isdayoff.ru";

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="IsDayOffService"/>.
    /// </summary>
    /// <param name="httpClient">HTTP-клиент (опционально).</param>
    public IsDayOffService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc />
    public async Task<CalendarData> GetCalendarDataAsync(int year)
    {
        // Получение данных для 5-дневной недели (коллекция int)
        var baseYearData = await GetYearDataAsync(year, pre: true);
        // Получение данных для 6-дневной недели
        var sixDayWeekData = await GetYearDataAsync(year, sd: true, pre: true);

        // Обработка данных и получение готовых списков
        var (nonworkingDays, nonworkingDays6, workingDays, shortenedDays, shortenedDays6) =
            ProcessYearData(year, baseYearData, sixDayWeekData);

        // Создание неизменяемого объекта CalendarData
        return new CalendarData
        {
            Year = year,
            NonworkingDays = nonworkingDays.AsReadOnly(),
            NonworkingDays6 = nonworkingDays6.AsReadOnly(),
            WorkingDays = workingDays.AsReadOnly(),
            ShortenedDays = shortenedDays.AsReadOnly(),
            ShortenedDays6 = shortenedDays6.AsReadOnly()
        };
    }

    /// <summary>
    /// Выполняет запрос к API isdayoff.ru и возвращает коллекцию кодов дней для указанного года.
    /// </summary>
    /// <param name="year">Год.</param>
    /// <param name="sd">Признак шестидневной рабочей недели.</param>
    /// <param name="pre">Признак учёта предпраздничных сокращённых дней.</param>
    /// <returns>Коллекция целых чисел (0,1,2,4), соответствующих каждому дню года.</returns>
    /// <exception cref="HttpRequestException">Ошибка HTTP-запроса.</exception>
    private async Task<IReadOnlyCollection<int>> GetYearDataAsync(int year, bool sd = false, bool pre = false)
    {
        var url = $"https://isdayoff.ru/api/getdata?year={year}&cc=ru";
        if (sd)
        {
            url += "&sd=1";
        }
        else if (pre)
        {
            url += "&pre=1";
        }

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        // Преобразование строки цифр в массив int
        return content.Select(c => c - '0').ToArray();
    }

    /// <summary>
    /// Обрабатывает данные за год и возвращает готовые коллекции нерабочих, рабочих и сокращённых дней.
    /// </summary>
    /// <param name="year">Год.</param>
    /// <param name="baseData">Коды дней для 5-дневной недели.</param>
    /// <param name="sixDayData">Коды дней для 6-дневной недели.</param>
    /// <returns>Кортеж, содержащий пять списков строк (формат MMdd).</returns>
    private static (List<string> NonworkingDays, List<string> NonworkingDays6, List<string> WorkingDays, List<string> ShortenedDays, List<string> ShortenedDays6)
        ProcessYearData(int year, IReadOnlyCollection<int> baseData, IReadOnlyCollection<int> sixDayData)
    {
        // Используем HashSet для автоматического обеспечения уникальности
        var nonworkingDays = new HashSet<string>();
        var nonworkingDays6 = new HashSet<string>();
        var workingDays = new HashSet<string>();
        var shortenedDays = new HashSet<string>();
        var shortenedDays6 = new HashSet<string>();

        int[] baseArray = baseData.ToArray();
        int[] sixDayArray = sixDayData.ToArray();

        for (int i = 0; i < baseArray.Length; i++)
        {
            DateOnly currentDate = new(year, 1, 1);
            currentDate = currentDate.AddDays(i);
            string mmdd = currentDate.ToString("MMdd");
            int dayOfWeek = (int)currentDate.DayOfWeek; // 0 - воскресенье, 6 - суббота

            // Для 5-дневной недели
            switch (baseArray[i])
            {
                case 1 when !IsRegularWeekend(dayOfWeek, false):
                    nonworkingDays.Add(mmdd);
                    break;
                case 2:
                    shortenedDays.Add(mmdd);
                    break;
                case 4:
                    workingDays.Add(mmdd);
                    break;
            }

            // Для 6-дневной недели
            switch (sixDayArray[i])
            {
                case 1 when dayOfWeek is not 0:
                    nonworkingDays6.Add(mmdd);
                    break;
                case 2:
                    shortenedDays6.Add(mmdd);
                    break;
            }
        }

        // Сортируем и преобразуем в списки
        return (
            nonworkingDays.OrderBy(x => x).ToList(),
            nonworkingDays6.OrderBy(x => x).ToList(),
            workingDays.OrderBy(x => x).ToList(),
            shortenedDays.OrderBy(x => x).ToList(),
            shortenedDays6.OrderBy(x => x).ToList()
        );
    }

    /// <summary>
    /// Определяет, является ли день стандартным выходным для заданного типа недели.
    /// </summary>
    /// <param name="dayOfWeek">Номер дня недели (0=вс, 6=сб).</param>
    /// <param name="isSixDayWeek">Признак шестидневной недели.</param>
    /// <returns>true, если день является стандартным выходным; иначе false.</returns>
    private static bool IsRegularWeekend(int dayOfWeek, bool isSixDayWeek)
    {
        return isSixDayWeek
            ? dayOfWeek is 0          // только воскресенье
            : dayOfWeek is 0 or 6;    // суббота и воскресенье
    }
}
