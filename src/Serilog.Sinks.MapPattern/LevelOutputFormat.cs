using Serilog.Events;

namespace Serilog.Sinks.MapPattern;

internal static class LevelOutputFormat
{
    private static readonly string[][] _titleCaseLevelMap =
    [
        ["V", "Vb", "Vrb", "Verb", "Verbo", "Verbos", "Verbose"],
        ["D", "De", "Dbg", "Dbug", "Debug"],
        [
            "I", "In", "Inf", "Info", "Infor", "Inform", "Informa", "Informat", "Informati", "Informatio",
            "Information",
        ],
        ["W", "Wn", "Wrn", "Warn", "Warni", "Warnin", "Warning"],
        ["E", "Er", "Err", "Eror", "Error"],
        ["F", "Fa", "Ftl", "Fatl", "Fatal"],
    ];

    private static readonly string[][] _lowerCaseLevelMap =
    [
        ["v", "vb", "vrb", "verb", "verbo", "verbos", "verbose"],
        ["d", "de", "dbg", "dbug", "debug"],
        [
            "i", "in", "inf", "info", "infor", "inform", "informa", "informat", "informati", "informatio",
            "information",
        ],
        ["w", "wn", "wrn", "warn", "warni", "warnin", "warning"],
        ["e", "er", "err", "eror", "error"],
        ["f", "fa", "ftl", "fatl", "fatal"],
    ];

    private static readonly string[][] _upperCaseLevelMap =
    [
        ["V", "VB", "VRB", "VERB", "VERBO", "VERBOS", "VERBOSE"],
        ["D", "DE", "DBG", "DBUG", "DEBUG"],
        [
            "I", "IN", "INF", "INFO", "INFOR", "INFORM", "INFORMA", "INFORMAT", "INFORMATI", "INFORMATIO",
            "INFORMATION",
        ],
        ["W", "WN", "WRN", "WARN", "WARNI", "WARNIN", "WARNING"],
        ["E", "ER", "ERR", "EROR", "ERROR"],
        ["F", "FA", "FTL", "FATL", "FATAL"],
    ];

    public static string GetLevelMoniker(LogEventLevel value, string? format = null)
    {
        if (value < LogEventLevel.Verbose || value > LogEventLevel.Fatal)
        {
            return Casing.Format(value.ToString(), format);
        }

        if (format == null || (format.Length != 2 && format.Length != 3))
        {
            return Casing.Format(GetLevelMoniker(_titleCaseLevelMap, value), format);
        }

        int num = format[1] - 48;
        if (format.Length == 3)
        {
            num *= 10;
            num += format[2] - 48;
        }

        if (num < 1)
        {
            return string.Empty;
        }

        return format[0] switch
        {
            'w' => GetLevelMoniker(_lowerCaseLevelMap, value, num),
            'u' => GetLevelMoniker(_upperCaseLevelMap, value, num),
            't' => GetLevelMoniker(_titleCaseLevelMap, value, num),
            _ => Casing.Format(GetLevelMoniker(_titleCaseLevelMap, value), format),
        };
    }

    private static string GetLevelMoniker(string[][] caseLevelMap, LogEventLevel level, int width)
    {
        string[] array = caseLevelMap[(int)level];
        return array[Math.Min(width, array.Length) - 1];
    }

    private static string GetLevelMoniker(string[][] caseLevelMap, LogEventLevel level)
    {
        return caseLevelMap[(int)level][^1];
    }
}