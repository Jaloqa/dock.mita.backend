using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Settings;

namespace Mita.Api.Features.Settings.UpdateSettings;

public sealed record UpdateSettingsRequest(
    JsonElement? BaseCurrency,
    JsonElement? Locale,
    JsonElement? MonthStartDay,
    JsonElement? Theme,
    JsonElement? Notifications);

public sealed record UpdateSettingsCommand(
    JsonElement? BaseCurrency,
    JsonElement? Locale,
    JsonElement? MonthStartDay,
    JsonElement? Theme,
    JsonElement? Notifications) : IRequest<Result<SettingsDto>>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [SettingsCacheTags.Settings];
}