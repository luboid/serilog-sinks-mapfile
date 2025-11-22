using Microsoft.Extensions.ObjectPool;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace Serilog.Sinks.MapPattern;

internal sealed class MappedSink : ILogEventSink, IDisposable
{
    private static readonly DefaultObjectPoolProvider _objectPoolProvider = new();
    private readonly Action<string, LoggerSinkConfiguration> _configure;
    private readonly int? _sinkMapCountLimit;
    private readonly bool _sinkMapReleaseOnRollout;
    private readonly PatternTemplateTextFormatter _patternTemplate;
    private readonly ConcurrentDictionary<MappedSinkEntry, Timer> _timers = [];
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, MappedSinkEntry>> _sinks = [];
    private readonly ObjectPool<StringWriter> _stringWriterPool;
    private readonly Timer? _sinkCountLimitTimer;
    private volatile bool _disposed;

    public MappedSink(
        PatternTemplate patternTemplate,
        IFormatProvider? formatProvider,
        Action<string, LoggerSinkConfiguration> configure,
        int? sinkMapCountLimit = null,
        bool sinkMapReleaseOnRollout = true)
    {
        _stringWriterPool = _objectPoolProvider.Create(new StringWriterPooledObjectPolicy());
        _patternTemplate = new PatternTemplateTextFormatter(patternTemplate, formatProvider);
        _configure = configure;
        _sinkMapCountLimit = sinkMapCountLimit;
        _sinkMapReleaseOnRollout = sinkMapReleaseOnRollout;
        if (_sinkMapCountLimit > 0)
        {
            _sinkCountLimitTimer = new Timer(
                CheckSinksLimit,
                null,
                100, // start after 100ms
                1000); // on every second
        }
    }

    public void Emit(LogEvent logEvent)
    {
        ThrowIfDisposed();
        GetPattern(logEvent, out string patternKey, out string pattern);
        ILogEventSink sink = EnsureSink(patternKey, pattern);
        try
        {
            sink?.Emit(logEvent);
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _sinkCountLimitTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _sinkCountLimitTimer?.Dispose();

        foreach ((_, Timer timer) in _timers)
        {
            try
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // timer is elapsed
                continue;
            }

            timer.Dispose();
        }

        foreach (MappedSinkEntry entry in _sinks.SelectMany(t => t.Value.Values))
        {
            entry.Dispose();
        }

        _sinks.Clear();
        _timers.Clear();
        (_stringWriterPool as IDisposable)?.Dispose();
    }

    private void CheckSinksLimit(object? obj)
    {
        if (_disposed)
        {
            return;
        }

        if (_sinkMapCountLimit.HasValue && _sinks.Count > _sinkMapCountLimit.Value)
        {
            int count = _sinks.Count - _sinkMapCountLimit.Value;
            foreach (MappedSinkEntry entry in _sinks.SelectMany(t => t.Value.Values).OrderBy(i => i.Created))
            {
                if (_disposed)
                {
                    break;
                }

                DisposeSink(entry);
                --count;
                if (count <= 0)
                {
                    break;
                }
            }
        }
    }

    private ILogEventSink EnsureSink(string patternKey, string pattern)
    {
        ThrowIfDisposed();
        int patternKeyHash = patternKey.GetHashCode();
        int patternHash = pattern.GetHashCode();
        ConcurrentDictionary<int, MappedSinkEntry> list = _sinks.GetOrAdd(patternKeyHash, (_) => []);
        return list.GetOrAdd(patternHash, (patternHash) =>
        {
            MappedSinkEntry entry = CreateSink(patternKeyHash, patternHash, pattern);
            if (_sinkMapReleaseOnRollout)
            {
                ReleaseOthers(entry);
            }

            return entry;
        }).Sink;
    }

    private MappedSinkEntry CreateSink(int patternKeyHash, int patternHash, string pattern)
    {
        ThrowIfDisposed();
        ILogEventSink sink = LoggerSinkConfiguration.CreateSink((wt) => _configure(pattern, wt));
        return new MappedSinkEntry(patternKeyHash, patternHash, sink, DateTime.UtcNow);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("MappedSink", "The mapped sink has been disposed.");
        }
    }

    private void ReleaseOthers(MappedSinkEntry entry)
    {
        Timer timer = _timers.GetOrAdd(entry, (entry) =>
        {
            return new(
                (obj) =>
                {
                    MappedSinkEntry entry = (MappedSinkEntry)obj!;
                    if (!_timers.TryRemove(entry, out Timer? timer))
                    {
                        return;
                    }

                    timer.Dispose();
                    if (_sinks.TryGetValue(entry.PatternKeyHash, out ConcurrentDictionary<int, MappedSinkEntry>? sinks))
                    {
                        foreach ((int patternHash, MappedSinkEntry sink) in sinks)
                        {
                            if (patternHash == entry.PatternHash)
                            {
                                continue;
                            }

                            DisposeSink(sink);
                        }
                    }
                },
                entry,
                Timeout.Infinite,
                Timeout.Infinite);
        });

        timer.Change(10, Timeout.Infinite);
    }

    private void DisposeSink(MappedSinkEntry entry)
    {
        // dispose the old sink after 5 seconds to allow any in-flight writes to complete
        _timers.GetOrAdd(entry, (entry) =>
        {
            Timer timer = new(
                (obj) =>
                {
                    MappedSinkEntry entry = (MappedSinkEntry)obj!;
                    if (!_timers.TryRemove(entry, out Timer? timer))
                    {
                        return;
                    }

                    timer.Dispose();
                    if (_sinks.TryGetValue(entry.PatternKeyHash, out ConcurrentDictionary<int, MappedSinkEntry>? sinks)
                        && sinks.TryRemove(entry.PatternHash, out MappedSinkEntry? sink))
                    {
                        sink.Dispose();
                    }
                },
                entry,
                Timeout.Infinite,
                Timeout.Infinite);

            timer.Change(5000, Timeout.Infinite);

            return timer;
        });
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

    private sealed class MappedSinkEntry : IDisposable
    {
        public MappedSinkEntry(int patternKeyHash, int patternHash, ILogEventSink sink, DateTime created)
        {
            PatternKeyHash = patternKeyHash;
            PatternHash = patternHash;
            Sink = sink;
            Created = created;
        }

        public int PatternKeyHash { get; }

        public int PatternHash { get; }

        public ILogEventSink Sink { get; }

        public DateTime Created { get; }

        public void Dispose()
        {
            if (Sink is IDisposable disposableSink)
            {
                disposableSink.Dispose();
            }
        }
    }
}
