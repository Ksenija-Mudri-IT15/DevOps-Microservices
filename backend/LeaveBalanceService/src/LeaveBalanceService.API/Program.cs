using FluentValidation;
using LeaveBalanceService.Application.Interfaces;
using LeaveBalanceService.Application.Services;
using LeaveBalanceService.Application.DTOs;
using LeaveBalanceService.Application.Validators;
using LeaveBalanceService.Domain.Repositories;
using LeaveBalanceService.Infrastructure.Data;
using LeaveBalanceService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateLeaveBalanceDtoValidator>();
builder.Services.AddFluentValidationAutoValidation(); // From SharpGrip.FluentValidation.AutoValidation.Mvc

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infrastructure
builder.Services.AddDbContext<LeaveBalanceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ILeaveBalanceRepository, LeaveBalanceRepository>();

// Application
builder.Services.AddScoped<ILeaveBalanceService, LeaveBalanceService.Application.Services.LeaveBalanceService>();

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

// Global Exception Handler Middleware
// Note: In .NET 8, you would typically use IExceptionHandler, but an inline middleware works perfectly too.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (System.Exception ex)
    {
        Console.WriteLine($"[Error] {ex.Message}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("An unexpected error occurred. Please try again later.");
    }
});

using (var scope = app.Services.CreateScope())
{
    var _db = scope.ServiceProvider.GetRequiredService<LeaveBalanceDbContext>();
    _db.Database.Migrate();
}

app.Run();

// Needed for Integration Testing setup
public partial class Program { }

