using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Persistence;

public sealed class CurrencyMappingTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public void Should_StoreEveryCurrencyAsThreeLetterText()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var properties = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(Currency))
            .ToList();

        properties.ShouldNotBeEmpty();
        foreach (var property in properties)
        {
            property.GetProviderClrType().ShouldBe(typeof(string), $"{property.DeclaringType.DisplayName()}.{property.Name}");
            property.GetMaxLength().ShouldBe(3, $"{property.DeclaringType.DisplayName()}.{property.Name}");
        }
    }
}
