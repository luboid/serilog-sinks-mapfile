using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.MapPattern;

internal sealed class MappedSink : ILogEventSink, IDisposable
{
    private static readonly DefaultObjectPoolProvider _objectPoolProvider = new DefaultObjectPoolProvider();
    private readonly Action<string, LoggerSinkConfiguration> _configure;
    private readonly int? _sinkMapCountLimit;
    private readonly PatternTemplateTextFormatter _patternTemplate;
    private readonly ConcurrentDictionary<ILogEventSink, Timer> _timers = [];
    private readonly ConcurrentDictionary<int, (int PathHash, ILogEventSink Sink, DateTime Created)> _sinks = [];
    private readonly ObjectPool<StringWriter> _stringWriterPool;
    private bool _disposed;

    public MappedSink(PatternTemplate patternTemplate, IFormatProvider? formatProvider, Action<string, LoggerSinkConfiguration> configure, int? sinkMapCountLimit = null)
    {
        _stringWriterPool = _objectPoolProvider.Create(new StringWriterPooledObjectPolicy());
        _patternTemplate = new PatternTemplateTextFormatter(patternTemplate, formatProvider);
        _configure = configure;
        _sinkMapCountLimit = sinkMapCountLimit;
    }

    public void Emit(LogEvent logEvent)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("MappedSink", "The mapped sink has been disposed.");
        }

        GetPattern(logEvent, out string patternKey, out string pattern);
        ILogEventSink sink = EnsureSink(patternKey, pattern);
        try
        {
            sink?.Emit(logEvent);
        }
        finally
        {
            CheckSinksLimit();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach ((_, ILogEventSink sink, _) in _sinks.Values)
        {
            (sink as IDisposable)?.Dispose();
        }

        _sinks.Clear();

        foreach(var kvp in _timers)
        {
            try
            {
                kvp.Value.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // timer is elapsed
                return;
            }

            kvp.Value.Dispose();
            (kvp.Key as IDisposable)?.Dispose();
        }

        _timers.Clear();
        (_stringWriterPool as IDisposable)?.Dispose();
    }

    private void CheckSinksLimit()
    {
        if (_sinkMapCountLimit.HasValue && _sinks.Count > _sinkMapCountLimit.Value)
        {
            foreach (var i in _sinks.OrderBy(i => i.Value.Created))
            {
                if (_sinks.Count <= _sinkMapCountLimit.Value)
                {
                    return;
                }

                if (_sinks.TryRemove(i.Key, out (int PathHash, ILogEventSink Sink, DateTime Created) pathWithSink))
                {
                    DisposeSink(pathWithSink.Sink);
                }
            }
        }
    }

    private ILogEventSink EnsureSink(string patternKey, string pattern)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("MappedSink", "The mapped sink has been disposed.");
        }

        int currentPatternHash = pattern.GetHashCode();
        int patternKeyHash = patternKey.GetHashCode();
        (int pathHash, ILogEventSink sink, _) = _sinks.GetOrAdd(patternKeyHash, (_) =>
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("MappedSink", "The mapped sink has been disposed.");
            }

            ILogEventSink sink = LoggerSinkConfiguration.CreateSink((wt) => _configure(pattern, wt));
            return (currentPatternHash, sink, DateTime.UtcNow);
        });

        if (currentPatternHash != pathHash)
        {
            if (_sinks.TryRemove(patternKeyHash, out (int PathHash, ILogEventSink Sink, DateTime Created) pathWithSink))
            {
                DisposeSink(pathWithSink.Sink);
            }

            (_, sink, _) = _sinks.GetOrAdd(patternKeyHash, (_) =>
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException("MappedSink", "The mapped sink has been disposed.");
                }

                ILogEventSink sink = LoggerSinkConfiguration.CreateSink((wt) => _configure(pattern, wt));
                return (currentPatternHash, sink, DateTime.UtcNow);
            });
        }

        return sink;
    }

    private void DisposeSink(ILogEventSink sink)
    {
        Timer timer = _timers.GetOrAdd(sink, (sink) =>
        {
            return new(
                (obj) =>
                {
                    if (_timers.TryRemove((ILogEventSink)obj!, out Timer? timer))
                    {
                        timer?.Dispose();
                    }

                    (obj as IDisposable)?.Dispose();
                },
                sink,
                Timeout.Infinite,
                Timeout.Infinite);
        });

        // Dispose the old sink after 5 seconds to allow any in-flight writes to complete
        timer.Change(2000, Timeout.Infinite);
    }

    private void GetPattern(LogEvent logEvent, out string pathKey, out string path)
    {
        StringWriter pathWriter = _stringWriterPool.Get();
        StringWriter pathKeyWriter = _stringWriterPool.Get();
        try
        {
            _patternTemplate.Format(logEvent, pathKeyWriter, pathWriter);

            pathKey = pathKeyWriter.ToString();
            path = pathWriter.ToString();
        }
        finally
        {
            _stringWriterPool.Return(pathWriter);
            _stringWriterPool.Return(pathKeyWriter);
        }
    }

    private sealed class StringWriterPooledObjectPolicy : IPooledObjectPolicy<StringWriter>
    {
        public StringWriter Create()
        {
            return new StringWriter();
        }

        public bool Return(StringWriter obj)
        {
            obj.GetStringBuilder().Length = 0;
            return true;
        }
    }
}
