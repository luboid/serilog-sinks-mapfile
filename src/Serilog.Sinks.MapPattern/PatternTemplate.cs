namespace Serilog.Sinks.MapPattern;

public record PatternTemplate
{
    public required string Text { get; set; }

    public HashSet<int> RollingParameters { get; set; } = [];
}
