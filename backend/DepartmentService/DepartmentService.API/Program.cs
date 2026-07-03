using DepartmentService.API.Middleware;
using DepartmentService.Application.Services;
using DepartmentService.Application.Validators;
using DepartmentService.Domain.Repositories;
using DepartmentService.Infrastructure.Data;
using DepartmentService.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
app.Run();

public partial class Program { }
