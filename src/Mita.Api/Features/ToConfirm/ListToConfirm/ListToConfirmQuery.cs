using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.ToConfirm.ListToConfirm;

public sealed record ListToConfirmQuery(int Page, int PageSize) : IRequest<Result<PageResponse<ToConfirmItemDto>>>;
