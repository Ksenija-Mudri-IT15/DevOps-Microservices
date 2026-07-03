using Absence.Application.Interfaces;
using Absence.Application.Services;
using Absence.Infrastructure.Data;
using Absence.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Redirect("/swagger"));

// Automatically migrating in Docker environments
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AbsenceDbContext>();
    dbContext.Database.Migrate();
}

app.Run();

