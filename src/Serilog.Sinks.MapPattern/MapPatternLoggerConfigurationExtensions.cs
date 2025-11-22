using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.MapPattern;

namespace Serilog;

public static class MapPatternLoggerConfigurationExtensions
{
    public static LoggerConfiguration MapPattern(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        PatternTemplate patternTemplate,
        Action<string, LoggerSinkConfiguration> configure,
        IFormatProvider? formatProvider = null,
        int? sinkMapCountLimit = null,
        bool sinkMapReleaseOnRollout = true,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose,
        LoggingLevelSwitch? levelSwitch = null)
    {
        ArgumentNullException.ThrowIfNull(loggerSinkConfiguration);
        ArgumentNullException.ThrowIfNull(patternTemplate);
        ArgumentNullException.ThrowIfNull(configure);

        MappedSink mappedSink = new(
            patternTemplate,
            formatProvider,
            configure,
            sinkMapCountLimit,
            sinkMapReleaseOnRollout);

        return loggerSinkConfiguration.Sink(mappedSink, restrictedToMinimumLevel, levelSwitch);
    }
}