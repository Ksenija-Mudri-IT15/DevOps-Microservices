using RequestService.Application;
using RequestService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RequestService.API.Middleware;
using RequestService.Infrastructure.Data;
using Serilog;
using Serilog.Enrichers.Span;

const string ServiceName = "requestservice";

var builder = WebApplication.CreateBuilder(args);

// Logs: structured Serilog output, enriched with TraceId/SpanId so a log line can be
// correlated with the matching trace in Jaeger. Console for `docker compose logs`, Seq for search.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithProperty("Service", ServiceName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://seq"));

// Traces + metrics: OpenTelemetry. Traces go to Jaeger over OTLP; metrics are scraped by
// Prometheus from /metrics (RED method: request rate, error rate, duration all come for
// free from the ASP.NET Core + HttpClient instrumentation below).
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(ServiceName))
    .WithTracing(tracing => tracing
        .AddSource("MassTransit")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://jaeger:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddHealthChecks();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint(); // GET /metrics

using (var scope = app.Services.CreateScope())
{
    var _db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
    _db.Database.Migrate();
}

app.Run();

// For Integration Tests
public partial class Program { }

