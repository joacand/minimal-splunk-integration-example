using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "MinimalAspNetApp";
var tracingOtlpEndpoint = "http://splunk-collector:4317";

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.OpenTelemetry(
        endpoint: tracingOtlpEndpoint,
        protocol: Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc)
    .CreateLogger();

Serilog.Debugging.SelfLog.Enable(msg => Console.Error.WriteLine(msg));

builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(tracingOtlpEndpoint);
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        });
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(tracingOtlpEndpoint);
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        });
    });

var app = builder.Build();

app.MapGet("/", () =>
{
    Log.Information("Hello OpenTelemetry!");
    return "Hello OpenTelemetry!";
});

var cancellationTokenSource = new CancellationTokenSource();
var token = cancellationTokenSource.Token;

_ = Task.Run(async () =>
{
    while (!token.IsCancellationRequested)
    {
        Log.Information("Background log at {Time}", DateTimeOffset.Now);
        await Task.Delay(TimeSpan.FromSeconds(5), token);
    }
}, token);

app.Lifetime.ApplicationStopping.Register(() =>
{
    cancellationTokenSource.Cancel();
    Log.Information("Application stopping");
    Log.CloseAndFlush();
});

app.Run();
