using FluentValidation;
using FluentValidation.AspNetCore;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.API.Hubs;
using NotificationService.API.Middlewares;
using NotificationService.API.Realtime;
using NotificationService.Application.Consumers;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Application.Validators;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Realtime;
using NotificationService.Infrastructure.Repositories;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

const string ServiceName = "notificationservice";

var builder = WebApplication.CreateBuilder(args);

// Logs: structured Serilog output, enriched with TraceId/SpanId for correlation with traces (Jaeger).
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithProperty("Service", ServiceName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://seq"));

// Traces + metrics: OpenTelemetry. Traces to Jaeger over OTLP, metrics scraped by Prometheus from
// /metrics. AddSource("MassTransit") picks up the RabbitMQ publish/consume spans MassTransit
// already emits internally, so a trace started in RequestService continues through the queue.
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

// Infrastructure Layer: Database
// Use Postgres connection string from config, fallback to an in-memory db during testing if no config present
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    // For testability and out-of-box run if no DB is configured locally
    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseInMemoryDatabase("NotificationDb"));
}
else
{
    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Infrastructure Layer: Repositories
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Application Layer: Services
builder.Services.AddScoped<INotificationService, NotificationAppService>();

// Application Layer: Validation
builder.Services.AddValidatorsFromAssemblyContaining<CreateNotificationRequestValidator>();
builder.Services.AddFluentValidationAutoValidation(); // Automatic validation before controller action

// Asynchronous inter-service communication (RabbitMQ): reacts to events published by RequestService
var rabbitMqHost = builder.Configuration["RabbitMq:Host"] ?? "rabbitmq";
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<LeaveRequestCreatedConsumer>();
    x.AddConsumer<LeaveRequestStatusChangedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitMqHost);
        cfg.ConfigureEndpoints(context);
    });
});

// Reactive communication (System.Reactive + SignalR): every notification created
// (via REST or a RabbitMQ consumer) is pushed to connected clients in real time.
builder.Services.AddSingleton<INotificationEventPublisher, RxNotificationEventPublisher>();
builder.Services.AddHostedService<NotificationBroadcastService>();
builder.Services.AddSignalR();

// Local dev only: lets the frontend (a different origin) connect to the SignalR hub
// with credentials so it can be grouped by connection.
builder.Services.AddCors(options =>
{
    options.AddPolicy("NotificationHub", policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Auto-migrate on start
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
}

app.UseMiddleware<GlobalExceptionHandler>();

app.UseHttpsRedirection();

app.UseCors("NotificationHub");

app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint(); // GET /metrics

app.Run();

public partial class Program { }
