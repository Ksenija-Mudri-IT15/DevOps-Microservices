using DepartmentService.API.Middleware;
using DepartmentService.Application.Services;
using DepartmentService.Application.Validators;
using DepartmentService.Domain.Repositories;
using DepartmentService.Infrastructure.Data;
using DepartmentService.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

const string ServiceName = "departmentservice";

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure PostgreSQL via Npgsql
builder.Services.AddDbContext<DepartmentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<global::DepartmentService.Application.Services.IDepartmentService, global::DepartmentService.Application.Services.DepartmentService>();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateDepartmentValidator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DepartmentDbContext>();
    // Ensuring DB exists and applying any pending migrations
    dbContext.Database.Migrate();

    if (!dbContext.Departments.Any())
    {
        dbContext.Departments.Add(global::DepartmentService.Domain.Entities.Department.CreateDepartment("Human Resources", "Handles recruiting, onboarding, and employee relations."));
        dbContext.Departments.Add(global::DepartmentService.Domain.Entities.Department.CreateDepartment("Finance", "Manages company budgets, expenses, and payroll."));
        dbContext.Departments.Add(global::DepartmentService.Domain.Entities.Department.CreateDepartment("IT/Engineering", "Builds and maintains company software and infrastructure."));
        dbContext.Departments.Add(global::DepartmentService.Domain.Entities.Department.CreateDepartment("Administration", "Manages overall business operations and office coordination."));
        dbContext.SaveChanges();
    }
}

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint(); // GET /metrics
app.Run();

public partial class Program { }
