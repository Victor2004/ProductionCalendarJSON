namespace CalendarGenerator;

/// <summary>
/// Константы, возвращаемые API isdayoff.ru для каждого дня года
/// </summary>
public static class IsDayOffApiResponses
{
    /// <summary>Рабочий день</summary>
    public const int WorkingDay = 0;
    /// <summary>Нерабочий день (праздник или выходной)</summary>
    public const int NonWorkingDay = 1;
    /// <summary>Сокращённый рабочий день</summary>
    public const int ShortenedDay = 2;
    /// <summary>Рабочий день, выпадающий на выходной (перенесённый)</summary>
    public const int WorkingDayOnWeekend = 4;
}
