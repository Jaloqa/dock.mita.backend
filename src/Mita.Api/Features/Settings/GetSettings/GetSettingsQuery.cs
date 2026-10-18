using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Settings;

namespace Mita.Api.Features.Settings.GetSettings;

public sealed record GetSettingsQuery : IRequest<Result<SettingsDto>>, ICachedQuery
{
    public string CacheKey => SettingsCacheTags.Settings;

    public IReadOnlyCollection<string> CacheTags => [SettingsCacheTags.Settings];

    public TimeSpan? CacheExpiration => null;
}