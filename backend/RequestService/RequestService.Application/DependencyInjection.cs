using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RequestService.Application.Behaviors;

namespace RequestService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddAutoMapper(assembly);
        
        services.AddValidatorsFromAssembly(assembly);
        
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Add typed HTTP Client for internal microservice communication resolving by Hostname over Docker Network
        services.AddHttpClient<Services.IEmployeeServiceClient, Services.EmployeeServiceClient>(client => 
        {
            client.BaseAddress = new Uri("http://employeeservice:8080");
        });

        return services;
    }
}
