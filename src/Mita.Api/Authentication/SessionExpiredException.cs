namespace Mita.Api.Authentication;

public sealed class SessionExpiredException() : Exception("The token's security stamp is no longer valid.");
