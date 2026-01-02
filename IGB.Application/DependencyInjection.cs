using AutoMapper;
using FluentValidation;
using IGB.Application.Mappings;
using IGB.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace IGB.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // AutoMapper
        services.AddAutoMapper(typeof(MappingProfile));

        // FluentValidation
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Register services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<IApprovalService, ApprovalService>();

        return services;
    }
}

