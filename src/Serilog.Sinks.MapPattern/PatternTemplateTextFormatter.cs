using System.Globalization;
using Serilog.Events;
using Serilog.Parsing;

namespace Serilog.Sinks.MapPattern;

public class PatternTemplateTextFormatter
{
    private static readonly Dictionary<string, Action<PropertyToken, LogEvent, TextWriter, IFormatProvider?>> _handlers = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly HashSet<int> _rollingParameters;
    private readonly IFormatProvider? _formatProvider;
    private readonly MessageTemplate _outputTemplateParsed;

    static PatternTemplateTextFormatter()
    {
        _handlers["Level"] = (propertyToken, logEvent, output, _) =>
        {
            string levelMoniker = LevelOutputFormat.GetLevelMoniker(logEvent.Level, propertyToken.Format);
            output.Write(levelMoniker);
        };

        _handlers["TraceId"] = (propertyToken, logEvent, output, _) => output.Write(logEvent.TraceId?.ToString() ?? "TraceId");
        _handlers["SpanId"] = (propertyToken, logEvent, output, _) => output.Write(logEvent.TraceId?.ToString() ?? "SpanId");
        _handlers["Timestamp"] = (propertyToken, logEvent, output, formatProvider) => output.Write(logEvent.Timestamp.ToString(propertyToken.Format, formatProvider ?? CultureInfo.InvariantCulture));
        _handlers["UtcTimestamp"] = (propertyToken, logEvent, output, formatProvider) => output.Write(logEvent.Timestamp.UtcDateTime.ToString(propertyToken.Format, formatProvider ?? CultureInfo.InvariantCulture));
    }

    public PatternTemplateTextFormatter(PatternTemplate outputTemplate, IFormatProvider? formatProvider = null)
    {
        ArgumentNullException.ThrowIfNull(outputTemplate);
        ArgumentNullException.ThrowIfNull(outputTemplate.Text);

        _outputTemplateParsed = new MessageTemplateParser().Parse(outputTemplate.Text);
        _rollingParameters = outputTemplate.RollingParameters ?? [];
        _formatProvider = formatProvider ?? CultureInfo.InvariantCulture;
    }

    public void Format(LogEvent logEvent, TextWriter pathKey, TextWriter path)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(pathKey);

        int parameterIndex = -1;
        foreach (MessageTemplateToken messageTemplateToken in _outputTemplateParsed.Tokens)
        {
            if (messageTemplateToken is TextToken tt)
            {
                path.Write(tt.Text);
                pathKey.Write(tt.Text);
                continue;
            }

            ++parameterIndex;
            PropertyToken propertyToken = (PropertyToken)messageTemplateToken;
            if (_handlers.TryGetValue(propertyToken.PropertyName, out var handler))
            {
                handler(propertyToken, logEvent, path, _formatProvider);
                if (!_rollingParameters.Contains(parameterIndex))
                {
                    handler(propertyToken, logEvent, pathKey, _formatProvider);
                }

                continue;
            }

            if (!logEvent.Properties.TryGetValue(propertyToken.PropertyName, out LogEventPropertyValue? value2))
            {
                continue;
            }

            if (value2 is ScalarValue { Value: string value3 })
            {
                string value4 = Casing.Format(value3, propertyToken.Format);
                path.Write(value4);
                if (!_rollingParameters.Contains(parameterIndex))
                {
                    pathKey.Write(value4);
                }
            }
            else
            {
                value2.Render(path, propertyToken.Format, _formatProvider);
                if (!_rollingParameters.Contains(parameterIndex))
                {
                    value2.Render(pathKey, propertyToken.Format, _formatProvider);
                }
            }
        }
    }
}
