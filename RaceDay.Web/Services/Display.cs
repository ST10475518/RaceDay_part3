namespace RaceDay.Web.Services;

/// <summary>Small presentation helpers used by the views.</summary>
public static class Display
{
    public static string Ordinal(int n)
    {
        var suffix = ((n % 100) is >= 11 and <= 13)
            ? "th"
            : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    public static string Time(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";

    public static string TimeFull(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    public static string Icon(string type) => type switch
    {
        "Run" => "🏃",
        "Walk" => "🚶",
        "Cycle" => "🚴",
        _ => "🏁"
    };

    public static string Distance(decimal km) => km.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " km";

    public static string Date(DateTime d) => d.ToString("ddd, dd MMM yyyy · HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    public static string ShortDate(DateTime d) => d.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
