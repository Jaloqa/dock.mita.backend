using System.Text.RegularExpressions;

namespace Mita.Domain.Money;

public static partial class Currencies
{
    public static IReadOnlyList<Currency> All { get; } = Enum.GetValues<Currency>();

    public static IReadOnlyList<Currency> Base { get; } = [Currency.PLN, Currency.EUR, Currency.USD, Currency.UAH];

    public static IReadOnlyList<Currency> Quoted { get; } = [.. All.Where(currency => currency != Currency.PLN)];

    public static bool IsBase(this Currency currency) => Base.Contains(currency);

    public static bool TryParse(string? code, out Currency currency)
    {
        currency = default;
        return code is not null
            && ThreeUpperLetters().IsMatch(code)
            && Enum.TryParse(code, ignoreCase: false, out currency);
    }

    [GeneratedRegex(@"^[A-Z]{3}\z")]
    private static partial Regex ThreeUpperLetters();
}
