using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalendarGenerator
{
    public class CalendarData
    {
        [JsonPropertyName("Year")]
        public int Year { get; set; }

        [JsonPropertyName("NonworkingDays")]
        public List<string> NonworkingDays { get; set; } = new();

        [JsonPropertyName("NonworkingDays6")]
        public List<string> NonworkingDays6 { get; set; } = new();

        [JsonPropertyName("WorkingDays")]
        public List<string> WorkingDays { get; set; } = new();

        [JsonPropertyName("ShortenedDays")]
        public List<string> ShortenedDays { get; set; } = new();

        [JsonPropertyName("ShortenedDays6")]
        public List<string> ShortenedDays6 { get; set; } = new();
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
            var calendarData = new CalendarData { Year = year };

            // Получение данных для 5-дневной недели
            string baseYearData = await GetYearDataAsync(year, pre: true);
            // Получение данных для 6-дневной недели
            string sixDayWeekData = await GetYearDataAsync(year, sd: true, pre: true);

            // Обрабатываем оба набора данных
            ProcessYearData(calendarData, baseYearData, sixDayWeekData);

            return calendarData;
        }

        private async Task<string> GetYearDataAsync(int year, bool sd = false, bool pre = false)
        {
            string url = $"https://isdayoff.ru/api/getdata?year={year}&cc=ru";
            if (sd) url += "&sd=1";
            if (pre) url += "&pre=1";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        private void ProcessYearData(CalendarData data, string baseData, string sixDayData)
        {
            // Словари для хранения кодов дней по дате (MMdd)
            var baseDayCodes = new Dictionary<string, char>();
            var sixDayCodes = new Dictionary<string, char>();

            // Заполнение словарей
            for (int i = 0; i < baseData.Length; i++)
            {
                DateOnly currentDate = new DateOnly(data.Year, 1, 1).AddDays(i);
                string mmdd = currentDate.ToString("MMdd");
                baseDayCodes[mmdd] = baseData[i];
            }

            for (int i = 0; i < sixDayData.Length; i++)
            {
                DateOnly currentDate = new DateOnly(data.Year, 1, 1).AddDays(i);
                string mmdd = currentDate.ToString("MMdd");
                sixDayCodes[mmdd] = sixDayData[i];
            }

            // Обрабатывается каждый день года
            foreach (var kvp in baseDayCodes)
            {
                string mmdd = kvp.Key;
                char baseCode = kvp.Value;
                char sixDayCode = sixDayCodes[mmdd];

                // Определение деня недели
                DateOnly date = new DateOnly(data.Year, int.Parse(mmdd.Substring(0, 2)), int.Parse(mmdd.Substring(2, 2)));
                int dayOfWeek = (int)date.DayOfWeek; // 0 - воскресенье, 6 - суббота

                // Для 5-дневной недели
                switch (baseCode)
                {
                    case '1': // Нерабочий день (только праздники)
                        if (!IsRegularWeekend(dayOfWeek, false) || IsHoliday(date))
                        {
                            AddIfNotExists(data.NonworkingDays, mmdd);
                        }
                        break;
                    case '2': // Сокращенный день
                        AddIfNotExists(data.ShortenedDays, mmdd);
                        break;
                    case '4': // Рабочий день в выходной (перенос)
                        AddIfNotExists(data.WorkingDays, mmdd);
                        break;
                }

                // Для 6-дневной недели
                switch (sixDayCode)
                {
                    case '1': // Нерабочий день (для 6-дневки только воскресенья выходные)
                        if (dayOfWeek != 0 || IsHoliday(date)) // 0 - воскресенье
                        {
                            AddIfNotExists(data.NonworkingDays6, mmdd);
                        }
                        break;
                    case '2': // Сокращенный день
                        AddIfNotExists(data.ShortenedDays6, mmdd);
                        break;
                }
            }
        }

        private bool IsRegularWeekend(int dayOfWeek, bool isSixDayWeek)
        {
            if (isSixDayWeek)
            {
                // Для 6-дневной недели только воскресенье выходной
                return dayOfWeek == 0;
            }
            else
            {
                // Для 5-дневной недели суббота и воскресенье выходные
                return dayOfWeek == 0 || dayOfWeek == 6;
            }
        }

        private bool IsHoliday(DateOnly date)
        {
            // Проверяет является ли дата официальным праздником (не регулярным выходным)
            return (date.Month == 1 && date.Day >= 1 && date.Day <= 8) || // Новогодние каникулы
                   (date.Month == 3 && date.Day == 8) || // 8 марта
                   (date.Month == 5 && date.Day == 1) || // 1 мая
                   (date.Month == 5 && date.Day == 9) || // 9 мая
                   (date.Month == 6 && date.Day == 12) || // День России
                   (date.Month == 11 && date.Day == 4) || // День народного единства
                   (date.Month == 12 && date.Day == 31); // 31 декабря
        }

        private void AddIfNotExists(List<string> list, string mmdd)
        {
            if (!list.Contains(mmdd))
            {
                list.Add(mmdd);
                list.Sort(); // Сортировка для соответствия ожидаемому формату
            }
        }
    }

    // Временные заглушки для других сервисов
    public class ExampleService1 : ICalendarService
    {
        public string Name => "ExampleService1";

        public Task<CalendarData> GetCalendarDataAsync(int year)
        {
            var calendarData = new CalendarData { Year = year };

            // Заполнение данными
            calendarData.NonworkingDays.Add("0101");
            calendarData.WorkingDays.Add("0108");

            return Task.FromResult(calendarData);
        }
    }

    public class ExampleService2 : ICalendarService
    {
        public string Name => "ExampleService2";

        public Task<CalendarData> GetCalendarDataAsync(int year)
        {
            var calendarData = new CalendarData { Year = year };

            return Task.FromResult(calendarData);
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
                ICalendarService service = serviceName != null
                    ? AvailableServices.FirstOrDefault(s => s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                    : GetServiceFromUser();

                if (service == null)
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
}