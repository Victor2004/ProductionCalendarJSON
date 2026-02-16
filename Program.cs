using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace CalendarGenerator;

/// <summary>
/// Представляет данные производственного календаря за указанный год.
/// </summary>
public class CalendarData
{
    /// <summary>
    /// Год, для которого сгенерирован календарь.
    /// </summary>
    public required int Year { get; init; }

    /// <summary>
    /// Нерабочие дни для 5-дневной рабочей недели (формат MMdd).
    /// </summary>
    public required IReadOnlyCollection<string> NonworkingDays { get; init; }

    /// <summary>
    /// Нерабочие дни для 6-дневной рабочей недели (формат MMdd).
    /// </summary>
    public required IReadOnlyCollection<string> NonworkingDays6 { get; init; }

    /// <summary>
    /// Рабочие дни, выпадающие на выходные (переносы, формат MMdd).
    /// </summary>
    public required IReadOnlyCollection<string> WorkingDays { get; init; }

    /// <summary>
    /// Сокращённые рабочие дни для 5-дневной недели (формат MMdd).
    /// </summary>
    public required IReadOnlyCollection<string> ShortenedDays { get; init; }

    /// <summary>
    /// Сокращённые рабочие дни для 6-дневной недели (формат MMdd).
    /// </summary>
    public required IReadOnlyCollection<string> ShortenedDays6 { get; init; }
}

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
        if (pre)
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
            DateOnly currentDate = new DateOnly(year, 1, 1).AddDays(i);
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

/// <summary>
/// Основной класс программы, точка входа и обработка командной строки.
/// </summary>
class Program
{
    private static readonly List<ICalendarService> AvailableServices = new()
    {
        new IsDayOffService(),
        new ExampleService1(),
        new ExampleService2()
    };

    /// <summary>
    /// Точка входа в приложение.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код возврата.</returns>
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

    /// <summary>
    /// Генерирует календарь и сохраняет его в JSON-файл.
    /// </summary>
    /// <param name="yearInput">Год (может быть null, тогда запрашивается у пользователя).</param>
    /// <param name="serviceName">Имя сервиса (может быть null, тогда выбирается пользователем).</param>
    /// <param name="outputPath">Путь для сохранения (может быть null, тогда формируется автоматически).</param>
    private static async Task GenerateCalendarAsync(int? yearInput, string? serviceName, string? outputPath)
    {
        try
        {
            // Определяем год
            int year = yearInput ?? GetYearFromUser();

            // Выбираем сервис
            ICalendarService service;
            if (serviceName is not null)
            {
                // Используем промежуточную nullable переменную
                ICalendarService? foundService = AvailableServices.FirstOrDefault(s => s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                if (foundService is not null)
                {
                    service = foundService;
                }
                else
                {
                    Console.WriteLine($"Сервис '{serviceName}' не найден. Используется сервис по умолчанию.");
                    service = AvailableServices[0];
                }
            }
            else
            {
                service = GetServiceFromUser();
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

    /// <summary>
    /// Получает год от пользователя в интерактивном режиме.
    /// </summary>
    /// <returns>Выбранный год (от 2000 до 2100) или текущий год по умолчанию.</returns>
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

    /// <summary>
    /// Предлагает пользователю выбрать сервис из списка доступных.
    /// </summary>
    /// <returns>Выбранный сервис.</returns>
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

    /// <summary>
    /// Выводит статистику по сгенерированному календарю.
    /// </summary>
    /// <param name="data">Данные календаря.</param>
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