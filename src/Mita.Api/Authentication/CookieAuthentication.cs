namespace Mita.Api.Authentication;

public static class CookieAuthentication
{
    private const string ItemKey = "Mita.CookieAuthenticated";

    public static void MarkCookieAuthenticated(this HttpContext context) => context.Items[ItemKey] = true;

    public static bool IsCookieAuthenticated(this HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is true;
}
