using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Tags;

namespace Mita.Api.Features.Tags.ListTags;

public sealed record ListTagsQuery(string? Search, int Page, int PageSize) : IRequest<Result<PageResponse<TagDto>>>;
