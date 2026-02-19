using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using CalendarGenerator.Dtos;

namespace CalendarGenerator.Services;

/// <summary>
/// Парсер производственного календаря с сайта nalog-nalog.ru.
/// </summary>
public class NalogRuParserService : ICalendarService
{
    /// <inheritdoc />
    public string Name => "nalog-nalog.ru";

    private readonly HttpClient _httpClient;

    // Новогодние каникулы (всегда добавляются)
    private static readonly List<(int Month, int Day)> NewYearHolidays =
        Enumerable.Range(1, 8).Select(day => (1, day)).ToList();

    // Остальные фиксированные праздники по ст. 112 ТК РФ
    private static readonly HashSet<(int Month, int Day)> OtherFixedHolidays = new()
    {
        (2, 23), (3, 8), (5, 1), (5, 9), (6, 12), (11, 4)
    };

    public NalogRuParserService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    /// <inheritdoc />
    public async Task<CalendarData> GetCalendarDataAsync(int year)
    {
        string fiveDayUrl = $"https://nalog-nalog.ru/proizvodstvennyj_kalendar/{year}/";
        string sixDayUrl = $"https://nalog-nalog.ru/proizvodstvennyj_kalendar/{year}-6/";

        var fiveDayHtml = await _httpClient.GetStringAsync(fiveDayUrl);
        var sixDayHtml = await _httpClient.GetStringAsync(sixDayUrl);

        var (fiveDayNonworkingRaw, fiveDayWorking, fiveDayShortened) = ParseCalendarPage(fiveDayHtml, year, isSixDayWeek: false);
        var (sixDayNonworkingRaw, _, sixDayShortened) = ParseCalendarPage(sixDayHtml, year, isSixDayWeek: true);

        var allNonworking = BuildNonworkingDays(year, fiveDayNonworkingRaw, isSixDayWeek: false);
        var allNonworking6 = BuildNonworkingDays(year, sixDayNonworkingRaw, isSixDayWeek: true);

        return new CalendarData
        {
            Year = year,
            NonworkingDays = allNonworking.OrderBy(x => x).ToList().AsReadOnly(),
            NonworkingDays6 = allNonworking6.OrderBy(x => x).ToList().AsReadOnly(),
            WorkingDays = fiveDayWorking.OrderBy(x => x).ToList().AsReadOnly(),
            ShortenedDays = fiveDayShortened.OrderBy(x => x).ToList().AsReadOnly(),
            ShortenedDays6 = sixDayShortened.OrderBy(x => x).ToList().AsReadOnly()
        };
    }

    /// <summary>
    /// Парсит HTML-страницу календаря и возвращает сырые данные (без учёта фильтрации праздников).
    /// </summary>
    private (HashSet<string> nonworking, HashSet<string> working, HashSet<string> shortened) ParseCalendarPage(
        string html, int year, bool isSixDayWeek)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var monthNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'calendar_month')]");
        if (monthNodes == null)
            return (new HashSet<string>(), new HashSet<string>(), new HashSet<string>());

        var nonworking = new HashSet<string>();
        var working = new HashSet<string>();
        var shortened = new HashSet<string>();

        foreach (var monthNode in monthNodes)
        {
            var monthNameNode = monthNode.SelectSingleNode(".//a[contains(@class, 'calendar_month_name')]");
            if (monthNameNode == null) continue;
            string monthName = monthNameNode.InnerText.Trim();
            int month = GetMonthNumber(monthName);
            if (month == 0) continue;

            var dayCells = monthNode.SelectNodes(".//div[contains(@class, 'calendar_day') and string-length(normalize-space(.)) > 0]");
            if (dayCells == null) continue;

            foreach (var cell in dayCells)
            {
                string dayText = cell.InnerText.Trim();
                if (!int.TryParse(dayText, out int day)) continue;

                string mmdd = $"{month:D2}{day:D2}";
                var date = new DateOnly(year, month, day);
                int dayOfWeek = (int)date.DayOfWeek; // 0 = воскресенье, 6 = суббота

                bool isRegularWeekend = isSixDayWeek ? dayOfWeek == 0 : (dayOfWeek == 0 || dayOfWeek == 6);

                if (cell.HasClass("holiday"))
                {
                    shortened.Add(mmdd);
                    if (isRegularWeekend)
                        working.Add(mmdd);
                }
                else if (cell.HasClass("festive"))
                {
                    nonworking.Add(mmdd);
                }
                else // обычный рабочий день
                {
                    if (isRegularWeekend)
                        working.Add(mmdd);
                }
            }
        }

        return (nonworking, working, shortened);
    }

    /// <summary>
    /// Формирует итоговый список нерабочих дней, комбинируя фиксированные праздники
    /// и данные из таблиц, с учётом регулярных выходных.
    /// </summary>
    private List<string> BuildNonworkingDays(int year, HashSet<string> parsedFestive, bool isSixDayWeek)
    {
        var result = new HashSet<string>();

        // Новогодние каникулы (всегда)
        foreach (var (month, day) in NewYearHolidays)
        {
            string mmdd = $"{month:D2}{day:D2}";
            result.Add(mmdd);
        }

        // Остальные фиксированные праздники – только если не на регулярном выходном
        foreach (var (month, day) in OtherFixedHolidays)
        {
            var date = new DateOnly(year, month, day);
            int dayOfWeek = (int)date.DayOfWeek;
            bool isRegularWeekend = isSixDayWeek ? dayOfWeek == 0 : (dayOfWeek == 0 || dayOfWeek == 6);
            if (!isRegularWeekend)
            {
                string mmdd = $"{month:D2}{day:D2}";
                result.Add(mmdd);
            }
        }

        // Дни из parsedFestive, не являющиеся регулярными выходными
        foreach (var mmdd in parsedFestive)
        {
            int month = int.Parse(mmdd.Substring(0, 2));
            int day = int.Parse(mmdd.Substring(2, 2));
            var date = new DateOnly(year, month, day);
            int dayOfWeek = (int)date.DayOfWeek;
            bool isRegularWeekend = isSixDayWeek ? dayOfWeek == 0 : (dayOfWeek == 0 || dayOfWeek == 6);
            if (!isRegularWeekend)
            {
                result.Add(mmdd);
            }
        }

        return result.ToList();
    }

    private int GetMonthNumber(string monthName)
    {
        return monthName.Trim().ToLower() switch
        {
            "январь" => 1,
            "февраль" => 2,
            "март" => 3,
            "апрель" => 4,
            "май" => 5,
            "июнь" => 6,
            "июль" => 7,
            "август" => 8,
            "сентябрь" => 9,
            "октябрь" => 10,
            "ноябрь" => 11,
            "декабрь" => 12,
            _ => 0
        };
    }
}
