using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

const string ServiceName = "apigateway";

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Logs: structured Serilog output, enriched with TraceId/SpanId. Since every request
// passes through the gateway first, its TraceId is the root of the whole distributed trace.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithProperty("Service", ServiceName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://seq"));

// Traces + metrics: OpenTelemetry. HttpClient instrumentation covers Ocelot's outgoing
// calls to downstream services, so a trace started here continues into each of them.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(ServiceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://jaeger:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseCors("CorsPolicy");

// WebApplication normally defers endpoint *execution* to the very end of the pipeline
// regardless of where Map* is called in code, so a terminal middleware registered later
// in code (UseOcelot(), which never calls next() for an unmatched path) still runs first
// and swallows /health and /metrics before routing gets a chance. Making UseRouting()/
// UseEndpoints() explicit pins endpoint execution to this exact point, ahead of Ocelot.
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapHealthChecks("/health");
    endpoints.MapPrometheusScrapingEndpoint(); // GET /metrics
});

app.UseOcelot().Wait();

app.Run();
