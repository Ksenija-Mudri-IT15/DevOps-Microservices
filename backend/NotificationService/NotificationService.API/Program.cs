using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.API.Middlewares;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Application.Validators;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
