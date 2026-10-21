using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Tags;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Tags.ListTags;

public sealed class ListTagsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListTagsQuery, Result<PageResponse<TagDto>>>
{
    public async Task<Result<PageResponse<TagDto>>> Handle(ListTagsQuery query, CancellationToken cancellationToken) =>
        await db.ProjectTags(currentUser.Id, query.Search)
            .ToPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
}
