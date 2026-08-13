using CircleTools.Application;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;

namespace CircleTools.Main.Service.Core.Application;

/// <summary>Query returning all heartbeats as DTOs.</summary>
public sealed record GetHeartbeatsQuery : IQuery<IReadOnlyList<HeartbeatDto>>;

/// <summary>Handles <see cref="GetHeartbeatsQuery"/> by mapping stored heartbeats to DTOs.</summary>
/// <param name="repository">The heartbeat repository port.</param>
public sealed class GetHeartbeatsQueryHandler(IHeartbeatRepository repository)
    : IQueryHandler<GetHeartbeatsQuery, IReadOnlyList<HeartbeatDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<HeartbeatDto>> HandleAsync(
        GetHeartbeatsQuery query, CancellationToken cancellationToken)
    {
        var heartbeats = await repository.ListAsync(cancellationToken);
        return heartbeats
            .Select(h => new HeartbeatDto(h.Id, h.CreatedAt, h.Note))
            .ToList();
    }
}
