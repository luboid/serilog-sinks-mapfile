using Serilog;
using Serilog.Core.Enrichers;

namespace WorkerServiceSerilog;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Enable Serilog self-logging for troubleshooting
        global::Serilog.Debugging.SelfLog.Enable(Console.Error);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();

        builder.Services.AddSerilog((services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.With(new PropertyEnricher("Service", "WorkerServiceSerilog"));
        });

        var host = builder.Build();

        await host.RunAsync();
    }
}