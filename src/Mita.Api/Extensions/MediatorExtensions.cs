using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Mita.Api.Behaviors;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Debts;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Transactions;

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
            options.AddOpenBehavior(typeof(CacheInvalidationBehavior<,>));
            options.AddOpenBehavior(typeof(CachingBehavior<,>));
        });
        services.AddValidatorsFromAssemblyContaining<Program>();
        services.AddScoped<TransactionWriter>();
        services.AddScoped<DebtWriter>();
        services.AddScoped<GroupAccess>();
        services.AddScoped<GroupExpenseWriter>();
        services.AddScoped<GroupRepaymentWriter>();
        services.AddSingleton<IHybridCacheSerializerFactory, ResultCacheSerializerFactory>();
        return services;
    }
}
