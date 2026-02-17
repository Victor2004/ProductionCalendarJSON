using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using CalendarGenerator.Dtos;
using CalendarGenerator.Services;

namespace CalendarGenerator;

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
