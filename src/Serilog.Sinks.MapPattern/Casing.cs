namespace Serilog.Sinks.MapPattern;

internal static class Casing
{
    public static string Format(string value, string? format = null)
    {
        if (format != "u")
        {
            if (format == "w")
            {
                return value.ToLowerInvariant();
            }

            return value;
        }

        return value.ToUpperInvariant();
    }
}