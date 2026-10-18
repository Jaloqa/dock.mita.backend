using FluentValidation;
using Mita.Api.Behaviors;

namespace Mita.Api.Extensions;

public static class MediatorExtensions
{
    public static IServiceCollection AddMitaMediator(this IServiceCollection services)
    {
        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblyContaining<Program>();
            options.AddOpenBehavior(typeof(LoggingBehavior<,>));
            options.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssemblyContaining<Program>();
        return services;
    }
}