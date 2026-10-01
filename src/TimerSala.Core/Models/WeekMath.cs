using System.Globalization;

namespace TimerSala.Core.Models;

public static class WeekMath
{
    public static DateOnly MondayOf(DateOnly date)
    {
        int offset = ((int)date.DayOfWeek + 6) % 7; // lunedì = 0
        return date.AddDays(-offset);
    }

    public static (int Year, int Week) IsoWeek(DateOnly monday)
    {
        var dt = monday.ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(dt), ISOWeek.GetWeekOfYear(dt));
    }

    public static string Label(DateOnly monday, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.GetCultureInfo("it-IT");
        var sunday = monday.AddDays(6);
        return monday.Month == sunday.Month
            ? $"{monday.Day}–{sunday.Day} {sunday.ToString("MMMM yyyy", culture)}"
            : $"{monday.ToString("d MMM", culture)} – {sunday.ToString("d MMM yyyy", culture)}";
    }
}
