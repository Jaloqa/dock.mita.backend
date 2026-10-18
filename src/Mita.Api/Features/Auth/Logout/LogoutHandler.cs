using MediatR;
using Microsoft.AspNetCore.Identity;
using Mita.Api.Common;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;

namespace Mita.Api.Features.Auth.Logout;

public sealed partial class LogoutHandler(
    ITokenService tokens,
    UserManager<AppUser> userManager,
    SecurityStampCache stamps,
    ILogger<LogoutHandler> logger)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var userId = command.AuthenticatedUserId;
        var hasRefreshToken = !string.IsNullOrWhiteSpace(command.RefreshToken);

        if (hasRefreshToken)
        {
            userId ??= await tokens.FindOwnerAsync(command.RefreshToken!, cancellationToken);
            await tokens.RevokeFamilyAsync(command.RefreshToken!, cancellationToken);
        }

        if (userId is not null)
        {
            await BumpSecurityStampAsync(userId.Value);
            await stamps.InvalidateAsync(userId.Value);
        }

        return Result.Success();
    }

    private async Task BumpSecurityStampAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return;
        }

        var result = await userManager.UpdateSecurityStampAsync(user);
        if (result.Succeeded)
        {
            return;
        }

        user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return;
        }

        result = await userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            LogStampUpdateFailed(logger, userId, string.Join(",", result.Errors.Select(error => error.Code)));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Security stamp update failed for user {UserId}: {ErrorCodes}")]
    private static partial void LogStampUpdateFailed(ILogger logger, Guid userId, string errorCodes);
}
