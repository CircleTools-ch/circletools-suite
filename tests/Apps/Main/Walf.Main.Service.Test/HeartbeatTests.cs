using Walf.Main.Service.Core.Application;
using Walf.Main.Service.Core.Domain;
using Walf.Main.Service.Test.TestSupport;

namespace Walf.Main.Service.Test;

/// <summary>Tests for the Heartbeat skeleton domain and its command/query handlers.</summary>
public sealed class HeartbeatTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyNote(string note) =>
        Assert.Throws<ArgumentException>(() => Heartbeat.Create(note, DateTimeOffset.UtcNow));

    [Fact]
    public void Create_SetsFields()
    {
        var now = DateTimeOffset.UtcNow;

        var heartbeat = Heartbeat.Create("  hello  ", now);

        Assert.Equal("hello", heartbeat.Note);
        Assert.Equal(now, heartbeat.CreatedAt);
        Assert.NotEqual(Guid.Empty, heartbeat.Id);
    }

    [Fact]
    public async Task CreateHandler_Adds_And_QueryHandler_MapsToDtos()
    {
        var repository = new InMemoryHeartbeatRepository();
        var createHandler = new CreateHeartbeatCommandHandler(repository, TimeProvider.System);
        var queryHandler = new GetHeartbeatsQueryHandler(repository);

        var id = await createHandler.HandleAsync(new CreateHeartbeatCommand("first"), CancellationToken.None);

        var dtos = await queryHandler.HandleAsync(new GetHeartbeatsQuery(), CancellationToken.None);

        var dto = Assert.Single(dtos);
        Assert.Equal(id, dto.Id);
        Assert.Equal("first", dto.Note);
        Assert.NotEqual(Guid.Empty, dto.Id);
    }
}
