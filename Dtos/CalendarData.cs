using System.Collections.Generic;

namespace CalendarGenerator.Dtos;

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
