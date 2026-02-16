using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace CalendarGenerator;

public class CalendarData
{
    public required int Year { get; init; }
    public required IReadOnlyCollection<string> NonworkingDays { get; init; }
    public required IReadOnlyCollection<string> NonworkingDays6 { get; init; }
    public required IReadOnlyCollection<string> WorkingDays { get; init; }
    public required IReadOnlyCollection<string> ShortenedDays { get; init; }
    public required IReadOnlyCollection<string> ShortenedDays6 { get; init; }
}

public interface ICalendarService
{
    string Name { get; }
    Task<CalendarData> GetCalendarDataAsync(int year);
}

public class IsDayOffService : ICalendarService
{
    public string Name => "isdayoff.ru";

    private readonly HttpClient _httpClient;

    public IsDayOffService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<CalendarData> GetCalendarDataAsync(int year)
    {
        // Временные изменяемые списки для сбора данных
        var nonworkingDays = new List<string>();
        var nonworkingDays6 = new List<string>();
        var workingDays = new List<string>();
        var shortenedDays = new List<string>();
        var shortenedDays6 = new List<string>();

        // Получение данных для 5-дневной недели (коллекция int)
        var baseYearData = await GetYearDataAsync(year, pre: true);
        // Получение данных для 6-дневной недели
        var sixDayWeekData = await GetYearDataAsync(year, sd: true, pre: true);

        // Обработка обоих наборов данных с заполнением временных списков
        ProcessYearData(year, baseYearData, sixDayWeekData,
            nonworkingDays, nonworkingDays6, workingDays, shortenedDays, shortenedDays6);

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

    // Метод теперь возвращает коллекцию целых чисел
    private async Task<IReadOnlyCollection<int>> GetYearDataAsync(int year, bool sd = false, bool pre = false)
    {
        var url = $"https://isdayoff.ru/api/getdata?year={year}&cc=ru";
        if (sd) url += "&sd=1";
        if (pre) url += "&pre=1";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        // Преобразуем строку цифр в массив int
        return content.Select(c => c - '0').ToArray();
    }

    // Принимает коллекции int вместо строк
    private void ProcessYearData(
        int year,
        IReadOnlyCollection<int> baseData,
        IReadOnlyCollection<int> sixDayData,
        List<string> nonworkingDays,
        List<string> nonworkingDays6,
        List<string> workingDays,
        List<string> shortenedDays,
        List<string> shortenedDays6)
    {
        // Словари для хранения кодов дней по дате (MMdd)
        var baseDayCodes = new Dictionary<string, int>();
        var sixDayCodes = new Dictionary<string, int>();

        // Для удобства индексации преобразуем коллекции в массивы
        int[] baseArray = baseData.ToArray();
        int[] sixDayArray = sixDayData.ToArray();

        // Заполнение словарей
        for (int i = 0; i < baseArray.Length; i++)
        {
            DateOnly currentDate = new DateOnly(year, 1, 1);
            currentDate = currentDate.AddDays(i);
            string mmdd = currentDate.ToString("MMdd");
            baseDayCodes[mmdd] = baseArray[i];
        }

        for (int i = 0; i < sixDayArray.Length; i++)
        {
            DateOnly currentDate = new DateOnly(year, 1, 1);
            currentDate = currentDate.AddDays(i);
            string mmdd = currentDate.ToString("MMdd");
            sixDayCodes[mmdd] = sixDayArray[i];
        }

        // Обработка каждого дня года
        foreach (var kvp in baseDayCodes)
        {
            string mmdd = kvp.Key;
            int baseCode = kvp.Value;
            int sixDayCode = sixDayCodes[mmdd];

            // Определение дня недели
            DateOnly date = new(year,
                int.Parse(mmdd.Substring(0, 2)),
                int.Parse(mmdd.Substring(2, 2)));
            var dayOfWeek = (int)date.DayOfWeek; // 0 - воскресенье, 6 - суббота

            // Для 5-дневной недели
            switch (baseCode)
            {
                case 1: // Нерабочий день (только праздники)
                    if (!IsRegularWeekend(dayOfWeek, false))
                    {
                        AddIfNotExists(nonworkingDays, mmdd);
                    }
                    break;
                case 2: // Сокращенный день
                    AddIfNotExists(shortenedDays, mmdd);
                    break;
                case 4: // Рабочий день в выходной (перенос)
                    AddIfNotExists(workingDays, mmdd);
                    break;
            }

            // Для 6-дневной недели
            switch (sixDayCode)
            {
                case 1: // Нерабочий день (для 6-дневки только воскресенья выходные)
                    if (dayOfWeek is not 0) // 0 - воскресенье
                    {
                        AddIfNotExists(nonworkingDays6, mmdd);
                    }
                    break;
                case 2: // Сокращенный день
                    AddIfNotExists(shortenedDays6, mmdd);
                    break;
            }
        }
    }

    private bool IsRegularWeekend(int dayOfWeek, bool isSixDayWeek)
    {
        return isSixDayWeek
            // Для 6-дневной недели только воскресенье выходной
            ? dayOfWeek is 0
            // Для 5-дневной недели суббота и воскресенье выходные
            : dayOfWeek is 0 or 6;
    }

    private void AddIfNotExists(List<string> list, string mmdd)
    {
        if (!list.Contains(mmdd))
        {
            list.Add(mmdd);
            list.Sort();
        }
    }
}

// Временные заглушки для других сервисов
public class ExampleService1 : ICalendarService
{
    public string Name => "ExampleService1";

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

public class ExampleService2 : ICalendarService
{
    public string Name => "ExampleService2";

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

class Program
{
    private static readonly List<ICalendarService> AvailableServices = new()
    {
        new IsDayOffService(),
        new ExampleService1(),
        new ExampleService2()
    };

    static async Task<int> Main(string[] args)
    {
        var yearOption = new Option<int?>(
            aliases: new[] { "-y", "--year" },
            description: "Год для генерации календаря")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var serviceOption = new Option<string?>(
            aliases: new[] { "-s", "--service" },
            description: "Выбор сервиса (по умолчанию: isdayoff.ru)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var outputOption = new Option<string?>(
            aliases: new[] { "-o", "--output" },
            description: "Путь для сохранения JSON файла (по умолчанию: текущая папка и название [год].json)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var rootCommand = new RootCommand("Генератор календарей рабочих дней")
        {
            yearOption,
            serviceOption,
            outputOption
        };

        rootCommand.SetHandler(async (context) =>
        {
            var year = context.ParseResult.GetValueForOption(yearOption);
            var serviceName = context.ParseResult.GetValueForOption(serviceOption);
            var outputPath = context.ParseResult.GetValueForOption(outputOption);
            var token = context.GetCancellationToken();

            await GenerateCalendarAsync(year, serviceName, outputPath);
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task GenerateCalendarAsync(int? yearInput, string? serviceName, string? outputPath)
    {
        try
        {
            // Определяем год
            int year = yearInput ?? GetYearFromUser();

            // Выбираем сервис
            ICalendarService service = serviceName is not null
                ? AvailableServices.FirstOrDefault(s => s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                : GetServiceFromUser();

            if (service is null)
            {
                Console.WriteLine($"Сервис '{serviceName}' не найден. Используется сервис по умолчанию.");
                service = AvailableServices[0];
            }

            Console.WriteLine($"Используется сервис {service.Name}");
            Console.WriteLine($"Генерация календаря за {year} год");

            // Получение данных
            var calendarData = await service.GetCalendarDataAsync(year);

            // Формирование имени файла
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = $"{year}.json";
            }
            else if (Directory.Exists(outputPath))
            {
                outputPath = Path.Combine(outputPath, $"{year}.json");
            }

            // Сохранение JSON
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(calendarData, options);
            await File.WriteAllTextAsync(outputPath, json);

            Console.WriteLine($"Календарь успешно сгенерирован и сохранен в файл: {outputPath}");
            PrintStatistics(calendarData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static int GetYearFromUser()
    {
        while (true)
        {
            int currentYear = DateTime.Now.Year;
            Console.Write($"Введите год (по умолчанию текущий {currentYear} год): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return currentYear;
            }

            if (int.TryParse(input, out int year) && year >= 2000 && year <= 2100)
            {
                return year;
            }

            Console.WriteLine("Некорректный год. Введите год от 2000 до 2100 или нажмите Enter для выбора текущего года.");
        }
    }

    private static ICalendarService GetServiceFromUser()
    {
        Console.WriteLine("\nДоступные сервисы:");
        for (int i = 0; i < AvailableServices.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {AvailableServices[i].Name}");
        }

        while (true)
        {
            Console.Write($"\nВыберите сервис (1-{AvailableServices.Count}, по умолчанию 1): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return AvailableServices[0];
            }

            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= AvailableServices.Count)
            {
                return AvailableServices[choice - 1];
            }

            Console.WriteLine("Некорректный выбор.");
        }
    }

    private static void PrintStatistics(CalendarData data)
    {
        Console.WriteLine("\nСтатистика:");
        Console.WriteLine($"Год: {data.Year}");
        Console.WriteLine($"Нерабочих дней (5-дневка): {data.NonworkingDays.Count}");
        Console.WriteLine($"Нерабочих дней (6-дневка): {data.NonworkingDays6.Count}");
        Console.WriteLine($"Рабочие дни (выпадающие на выходные дни): {data.WorkingDays.Count}");
        Console.WriteLine($"Сокращенных дней (5-дневка): {data.ShortenedDays.Count}");
        Console.WriteLine($"Сокращенных дней (6-дневка): {data.ShortenedDays6.Count}");
    }
}