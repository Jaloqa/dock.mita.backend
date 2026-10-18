namespace Mita.Api.Features.Rates.GetRates;

public sealed record RateDto(string Date, string From, string To, double Value);
