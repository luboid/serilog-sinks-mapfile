using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Sinks.File;
using Serilog.Sinks.MapPattern;
using System.Text;

namespace Serilog;

public static class MapFileLoggerConfigurationExtensions
{
    public static LoggerConfiguration MapFile(
        this LoggerSinkConfiguration sinkConfiguration,
        PatternTemplate patternTemplate,
        int? sinkMapCountLimit = null,
        bool sinkMapReleaseOnRollout = true,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose,
        string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        IFormatProvider? formatProvider = null,
        long? fileSizeLimitBytes = 1073741824L,
        LoggingLevelSwitch? levelSwitch = null,
        bool buffered = false,
        bool shared = false,
        TimeSpan? flushToDiskInterval = null,
        RollingInterval rollingInterval = RollingInterval.Infinite,
        bool rollOnFileSizeLimit = false,
        int? retainedFileCountLimit = 31,
        Encoding? encoding = null,
        FileLifecycleHooks? hooks = null,
        TimeSpan? retainedFileTimeLimit = null)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentNullException.ThrowIfNull(patternTemplate);
        ArgumentNullException.ThrowIfNull(outputTemplate);

        MessageTemplateTextFormatter outputFormatter = new(outputTemplate, formatProvider);

        return sinkConfiguration.MapPattern(patternTemplate, (pattern, loggerSinkConfiguration) =>
        {
            loggerSinkConfiguration.File(
                outputFormatter,
                pattern,
                restrictedToMinimumLevel,
                fileSizeLimitBytes,
                levelSwitch,
                buffered,
                shared,
                flushToDiskInterval,
                rollingInterval,
                rollOnFileSizeLimit,
                retainedFileCountLimit,
                encoding,
                hooks,
                retainedFileTimeLimit);
        },
        formatProvider,
        sinkMapCountLimit,
        sinkMapReleaseOnRollout,
        restrictedToMinimumLevel,
        levelSwitch);
    }

    public static LoggerConfiguration MapFile(
        this LoggerSinkConfiguration sinkConfiguration,
        ITextFormatter formatter,
        PatternTemplate patternTemplate,
        int? sinkMapCountLimit = null,
        bool sinkMapReleaseOnRollout = true,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose,
        IFormatProvider? formatProvider = null,
        long? fileSizeLimitBytes = 1073741824L,
        LoggingLevelSwitch? levelSwitch = null,
        bool buffered = false,
        bool shared = false,
        TimeSpan? flushToDiskInterval = null,
        RollingInterval rollingInterval = RollingInterval.Infinite,
        bool rollOnFileSizeLimit = false,
        int? retainedFileCountLimit = 31,
        Encoding? encoding = null,
        FileLifecycleHooks? hooks = null,
        TimeSpan? retainedFileTimeLimit = null)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentNullException.ThrowIfNull(patternTemplate);
        ArgumentNullException.ThrowIfNull(formatter);

        return sinkConfiguration.MapPattern(patternTemplate, (pattern, loggerSinkConfiguration) =>
        {
            loggerSinkConfiguration.File(
                formatter,
                pattern,
                restrictedToMinimumLevel,
                fileSizeLimitBytes,
                levelSwitch,
                buffered,
                shared,
                flushToDiskInterval,
                rollingInterval,
                rollOnFileSizeLimit,
                retainedFileCountLimit,
                encoding,
                hooks,
                retainedFileTimeLimit);
        },
        formatProvider,
        sinkMapCountLimit,
        sinkMapReleaseOnRollout,
        restrictedToMinimumLevel,
        levelSwitch);
    }
}