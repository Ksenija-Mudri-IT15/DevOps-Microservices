using Absence.Application.Interfaces;
using Absence.Application.Services;
using Absence.Infrastructure.Data;
using Absence.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

const string ServiceName = "absenceservice";

var builder = WebApplication.CreateBuilder(args);

// Logs: structured Serilog output, enriched with TraceId/SpanId for correlation with traces (Jaeger).
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithProperty("Service", ServiceName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://seq"));

// Traces + metrics: OpenTelemetry. Traces to Jaeger over OTLP, metrics scraped by Prometheus from /metrics.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(ServiceName))
    .WithTracing(tracing => tracing
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

// Infrastructure setup
builder.Services.AddDbContext<AbsenceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAbsenceRepository, AbsenceRepository>();
builder.Services.AddScoped<AbsenceService>();

// Validation setup map
builder.Services.AddValidatorsFromAssemblyContaining<Absence.Application.Validators.CreateAnnualLeaveValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint(); // GET /metrics

app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Redirect("/swagger"));

// Automatically migrating in Docker environments
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AbsenceDbContext>();
    dbContext.Database.Migrate();
}

app.Run();

