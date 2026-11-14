// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Repository.Test.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CircleTools.Circles.Repository.Test;

/// <summary>
/// The <c>circles</c> schema mapping and the repository's aggregate semantics, against real
/// Postgres (TP-003).
/// </summary>
/// <remarks>
/// These are the properties no unit test can establish, because they are properties of the mapping
/// and of the database: that a circle survives a round trip whole, that a save is a transaction,
/// that deletion cascades, and that two callers editing one circle cannot both win.
/// </remarks>
[Collection(SharedCirclesDatabase.CollectionName)]
public sealed class CircleRepositoryTests(CirclesPostgresFixture fixture)
{
    [Fact]
    public async Task Circle_SurvivesARoundTripWholeWithEveryPersonInvitationAndDecline()
    {
        var owner = Guid.CreateVersion7();
        var joiner = Guid.CreateVersion7();

        var circle = CircleFactory.WithOwner(owner, "Anna", "de-CH", "Müller", "Familie");
        CircleFactory.Join(circle, owner, joiner, "beat@example.test", "Beat", "fr-CH");
        var managed = CircleFactory.AddManaged(circle, owner, "Grosi", "de-CH");
        var pending = CircleFactory.Invite(circle, owner, "chiara@example.test");
        var declined = CircleFactory.Invite(circle, owner, "dora@example.test");
        Assert.True(circle.Decline(declined.Id, CircleFactory.Now).IsSuccess);

        await using (var writing = fixture.CreateProvider())
        {
            await Repository(writing).AddAsync(circle);
        }

        // A second provider, so nothing is answered from the change tracker that wrote it.
        await using var reading = fixture.CreateProvider();
        var reloaded = await Repository(reading).FindAsync(circle.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Müller", reloaded.Name);

        // The label is user data, stored in the language it was written in and never re-translated.
        Assert.Equal("Familie", reloaded.Label);
        Assert.Equal(CircleFactory.Now, reloaded.CreatedAt);

        Assert.Equal(3, reloaded.Memberships.Count);
        Assert.Equal(CircleRole.Owner, reloaded.RoleOf(owner));
        Assert.Equal(CircleRole.Member, reloaded.RoleOf(joiner));

        var reloadedManaged = reloaded.FindMembership(managed.Id);
        Assert.NotNull(reloadedManaged);
        Assert.True(reloadedManaged.IsManaged);
        Assert.Null(reloadedManaged.UserId);
        Assert.Equal("Grosi", reloadedManaged.DisplayName);
        Assert.Equal("de-CH", reloadedManaged.Language);

        var reloadedInvitation = Assert.Single(reloaded.Invitations);
        Assert.Equal(pending.Id, reloadedInvitation.Id);
        Assert.Equal("chiara@example.test", reloadedInvitation.Email);
        Assert.Equal(TestEmailHasher.Instance.Hash("chiara@example.test"), reloadedInvitation.EmailHash);
        Assert.Equal(pending.SecretHandle, reloadedInvitation.SecretHandle);
        Assert.Equal(CircleFactory.Now.AddDays(14), reloadedInvitation.ExpiresAt);

        var reloadedDecline = Assert.Single(reloaded.Declines);
        Assert.Equal(TestEmailHasher.Instance.Hash("dora@example.test"), reloadedDecline.EmailHash);
    }

    [Fact]
    public async Task ADeclinedInvitation_LeavesARowThatHoldsNoAddress()
    {
        // NFR-004 at the level where it can be checked: whatever the domain intends, the question
        // is what ends up in the row. A declined address is remembered as a hash for the cooldown
        // and in no other form — the circle has no further business with it.
        var owner = Guid.CreateVersion7();
        var circle = CircleFactory.WithOwner(owner);
        var invitation = CircleFactory.Invite(circle, owner, "dora@example.test");
        Assert.True(circle.Decline(invitation.Id, CircleFactory.Now).IsSuccess);

        await using var provider = fixture.CreateProvider();
        await Repository(provider).AddAsync(circle);

        var context = provider.GetRequiredService<CirclesDbContext>();

        // Scoped to this circle: a decline is circle data, and the shared container holds other
        // circles that the same address has declined.
        var stored = Assert.Single(await RowsFor<DeclineRecord>(context, circle.Id));

        Assert.Equal(TestEmailHasher.Instance.Hash("dora@example.test"), stored.EmailHash);
        Assert.Equal(CircleFactory.Now, stored.DeclinedAt);

        // The invitation that carried the address is gone rather than flagged.
        Assert.Empty(await context.Set<Invitation>().AsNoTracking().Where(i => i.Id == invitation.Id).ToListAsync());

        var columns = context.Model.FindEntityType(typeof(DeclineRecord))!
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(["CircleId", "DeclinedAt", "EmailHash", "Id"], columns);
    }

    [Fact]
    public async Task FailedCreate_LeavesNeitherCircleNorMembership()
    {
        // FR-016's atomicity criterion. An ownerless circle can never be administered, transferred
        // or deleted by anyone, so it must not exist even for the width of a failed save. The save
        // is made to fail by a name longer than the column, which is a database-side rejection
        // arriving after the INSERT batch has been built — exactly the shape of failure that would
        // leave a half-created circle if the two rows were written separately.
        var owner = Guid.CreateVersion7();
        var circle = CircleFactory.WithOwner(owner, name: new string('x', 300));

        await using var provider = fixture.CreateProvider();

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => Repository(provider).AddAsync(circle));

        await using var verifying = fixture.CreateProvider();
        var context = verifying.GetRequiredService<CirclesDbContext>();

        Assert.Null(await context.Circles.AsNoTracking().SingleOrDefaultAsync(c => c.Id == circle.Id));
        Assert.Empty(await context.Set<CircleMembership>().AsNoTracking().Where(m => m.UserId == owner).ToListAsync());
    }

    [Fact]
    public async Task Delete_TakesEveryPersonInvitationAndDeclineWithIt()
    {
        // FR-022: deletion is one transaction and no soft-delete state survives it.
        var owner = Guid.CreateVersion7();
        var joiner = Guid.CreateVersion7();

        var circle = CircleFactory.WithOwner(owner);
        CircleFactory.Join(circle, owner, joiner, "beat@example.test", "Beat");
        CircleFactory.AddManaged(circle, owner, "Grosi");
        CircleFactory.Invite(circle, owner, "chiara@example.test");
        var declined = CircleFactory.Invite(circle, owner, "dora@example.test");
        Assert.True(circle.Decline(declined.Id, CircleFactory.Now).IsSuccess);

        await using var provider = fixture.CreateProvider();
        await Repository(provider).AddAsync(circle);

        await using (var deleting = fixture.CreateProvider())
        {
            var repository = Repository(deleting);
            var loaded = await repository.FindAsync(circle.Id);
            await repository.DeleteAsync(loaded!);
        }

        await using var verifying = fixture.CreateProvider();
        var context = verifying.GetRequiredService<CirclesDbContext>();

        Assert.Null(await context.Circles.AsNoTracking().SingleOrDefaultAsync(c => c.Id == circle.Id));
        Assert.Empty(await RowsFor<CircleMembership>(context, circle.Id));
        Assert.Empty(await RowsFor<Invitation>(context, circle.Id));
        Assert.Empty(await RowsFor<DeclineRecord>(context, circle.Id));
    }

    [Fact]
    public async Task FailedDelete_LeavesEverythingInPlace()
    {
        // FR-022's "one transaction" criterion. A deletion is a circle row, every membership, every
        // invitation and every decline — five statements that must not be able to half-succeed and
        // leave a circle whose people are gone.
        //
        // The save is made to fail by a second, unrelated change in the same unit of work: another
        // circle given a name longer than its column. That is the realistic shape of the failure —
        // nothing goes wrong with the delete itself, something else in the transaction does.
        var owner = Guid.CreateVersion7();
        var joiner = Guid.CreateVersion7();

        var circle = CircleFactory.WithOwner(owner, name: "Doomed");
        CircleFactory.Join(circle, owner, joiner, "beat@example.test", "Beat");
        CircleFactory.AddManaged(circle, owner, "Grosi");
        CircleFactory.Invite(circle, owner, "chiara@example.test");
        var declined = CircleFactory.Invite(circle, owner, "dora@example.test");
        Assert.True(circle.Decline(declined.Id, CircleFactory.Now).IsSuccess);

        var bystander = CircleFactory.WithOwner(owner, name: "Bystander");

        await using var seeding = fixture.CreateProvider();
        await Repository(seeding).AddAsync(circle);
        await Repository(seeding).AddAsync(bystander);

        var statements = new List<string>();
        await using (var failing = LoggingContext(statements))
        {
            var repository = new EfCircleRepository(failing);

            var doomed = await repository.FindAsync(circle.Id);
            var alsoTracked = await repository.FindAsync(bystander.Id);
            Assert.True(alsoTracked!.Update(owner, new string('x', 300), label: null).IsSuccess);

            await Assert.ThrowsAnyAsync<DbUpdateException>(() => repository.DeleteAsync(doomed!));
        }

        // Without this the test could pass vacuously: if the failing statement ran first, the
        // deletion would never have been attempted and "everything is still there" would prove
        // nothing. The rows below survived a delete that was issued and then rolled back.
        Assert.Contains(statements, sql => sql.Contains("DELETE FROM circles.circles", StringComparison.Ordinal));

        await using var verifying = fixture.CreateProvider();
        var context = verifying.GetRequiredService<CirclesDbContext>();
        var survived = await Repository(verifying).FindAsync(circle.Id);

        Assert.NotNull(survived);
        Assert.Equal(3, survived.Memberships.Count);
        Assert.Single(survived.Invitations);
        Assert.Single(survived.Declines);
        Assert.Equal(3, (await RowsFor<CircleMembership>(context, circle.Id)).Count);

        // …and the change that broke the save is not there either.
        Assert.Equal("Bystander", (await Repository(verifying).FindAsync(bystander.Id))!.Name);
    }

    [Fact]
    public async Task Delete_LeavesOtherCirclesUntouched()
    {
        // The cascade is scoped to the circle. A person is in several circles, and deleting one of
        // them is not a statement about the others.
        var owner = Guid.CreateVersion7();
        var doomed = CircleFactory.WithOwner(owner, name: "Doomed");
        var survivor = CircleFactory.WithOwner(owner, name: "Survivor");

        await using var provider = fixture.CreateProvider();
        var repository = Repository(provider);
        await repository.AddAsync(doomed);
        await repository.AddAsync(survivor);

        await repository.DeleteAsync((await repository.FindAsync(doomed.Id))!);

        await using var verifying = fixture.CreateProvider();
        var reloaded = await Repository(verifying).FindAsync(survivor.Id);

        Assert.NotNull(reloaded);
        Assert.Single(reloaded.Memberships);
    }

    [Fact]
    public async Task Transfer_WhenTheTargetLeftConcurrently_Fails_AndTheCircleKeepsItsOwner()
    {
        // FR-020's "the target's membership ended concurrently" criterion, and the reason the
        // circle row carries a concurrency token at all. Both callers load the same circle; one
        // removes the target, the other makes them owner. Without the token the second write wins
        // silently and the circle ends up owned by a membership that no longer exists.
        var owner = Guid.CreateVersion7();
        var target = Guid.CreateVersion7();

        var circle = CircleFactory.WithOwner(owner);
        var targetMembership = CircleFactory.Join(circle, owner, target, "beat@example.test", "Beat");

        await using var seeding = fixture.CreateProvider();
        await Repository(seeding).AddAsync(circle);

        await using var first = fixture.CreateProvider();
        await using var second = fixture.CreateProvider();

        var removing = Repository(first);
        var transferring = Repository(second);

        var asRemover = await removing.FindAsync(circle.Id);
        var asTransferrer = await transferring.FindAsync(circle.Id);

        Assert.True(asRemover!.Remove(owner, targetMembership.Id).IsSuccess);
        await removing.SaveAsync(asRemover);

        Assert.True(asTransferrer!.TransferOwnership(owner, targetMembership.Id).IsSuccess);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => transferring.SaveAsync(asTransferrer));

        await using var verifying = fixture.CreateProvider();
        var settled = await Repository(verifying).FindAsync(circle.Id);

        Assert.NotNull(settled);
        Assert.Equal(owner, settled.Owner.UserId);
        Assert.Single(settled.Memberships);
    }

    [Fact]
    public async Task TwoConcurrentEdits_DoNotBothWin_EvenWhenOnlyChildrenChanged()
    {
        // The generalisation of the transfer race. A role change touches a membership row and not
        // the circle's, so a token that only noticed changes to the circle's own columns would let
        // both saves through. This is the assertion that the token covers the whole aggregate.
        var owner = Guid.CreateVersion7();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var circle = CircleFactory.WithOwner(owner);
        var firstMembership = CircleFactory.Join(circle, owner, first, "beat@example.test", "Beat");
        var secondMembership = CircleFactory.Join(circle, owner, second, "chiara@example.test", "Chiara");

        await using var seeding = fixture.CreateProvider();
        await Repository(seeding).AddAsync(circle);

        await using var providerA = fixture.CreateProvider();
        await using var providerB = fixture.CreateProvider();
        var a = Repository(providerA);
        var b = Repository(providerB);

        var asA = await a.FindAsync(circle.Id);
        var asB = await b.FindAsync(circle.Id);

        Assert.True(asA!.ChangeRole(owner, firstMembership.Id, CircleRole.Admin).IsSuccess);
        await a.SaveAsync(asA);

        Assert.True(asB!.ChangeRole(owner, secondMembership.Id, CircleRole.Admin).IsSuccess);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveAsync(asB));
    }

    [Fact]
    public async Task ASecondDeclineByTheSameAddress_ReplacesTheRecordRatherThanColliding()
    {
        // The domain drops the previous decline and writes a fresh one, which for a unique index on
        // (circle, address hash) is a delete and an insert of the same key inside one save. Worth
        // pinning: it is the one place the mapping's uniqueness rule meets the aggregate's.
        var owner = Guid.CreateVersion7();
        var circle = CircleFactory.WithOwner(owner);

        var first = CircleFactory.Invite(circle, owner, "dora@example.test");
        Assert.True(circle.Decline(first.Id, CircleFactory.Now).IsSuccess);

        await using var provider = fixture.CreateProvider();
        var repository = Repository(provider);
        await repository.AddAsync(circle);

        // Past the seven-day cooldown, so the address can be invited again.
        var later = CircleFactory.Now.AddDays(8);
        var loaded = await repository.FindAsync(circle.Id);
        Assert.NotNull(loaded);

        var second = CircleFactory.Invite(loaded, owner, "dora@example.test", now: later);
        Assert.True(loaded.Decline(second.Id, later).IsSuccess);
        await repository.SaveAsync(loaded);

        await using var verifying = fixture.CreateProvider();
        var reloaded = await Repository(verifying).FindAsync(circle.Id);

        var decline = Assert.Single(reloaded!.Declines);
        Assert.Equal(later, decline.DeclinedAt);
    }

    [Fact]
    public async Task Queries_FindCirclesByMembership_Ownership_InvitationHandleAndInvitedAddress()
    {
        var owner = Guid.CreateVersion7();
        var member = Guid.CreateVersion7();
        var stranger = Guid.CreateVersion7();

        var circle = CircleFactory.WithOwner(owner, name: "Queries");
        CircleFactory.Join(circle, owner, member, "beat@example.test", "Beat");
        var invitation = CircleFactory.Invite(circle, owner, "chiara@example.test");

        await using var provider = fixture.CreateProvider();
        var repository = Repository(provider);
        await repository.AddAsync(circle);

        await using var reading = fixture.CreateProvider();
        var queries = Repository(reading);

        Assert.Contains(circle.Id, (await queries.ListForUserAsync(owner)).Select(c => c.Id));
        Assert.Contains(circle.Id, (await queries.ListForUserAsync(member)).Select(c => c.Id));
        Assert.DoesNotContain(circle.Id, (await queries.ListForUserAsync(stranger)).Select(c => c.Id));

        Assert.Contains(circle.Id, (await queries.ListOwnedByAsync(owner)).Select(c => c.Id));
        Assert.DoesNotContain(circle.Id, (await queries.ListOwnedByAsync(member)).Select(c => c.Id));
        Assert.Equal(1, await queries.CountOwnedByAsync(owner));
        Assert.Equal(0, await queries.CountOwnedByAsync(member));

        Assert.Equal(1, await queries.CountCreatedSinceAsync(owner, CircleFactory.Now.AddHours(-1)));
        Assert.Equal(0, await queries.CountCreatedSinceAsync(owner, CircleFactory.Now.AddHours(1)));

        var byHandle = await queries.FindByInvitationHandleAsync(invitation.SecretHandle);
        Assert.Equal(circle.Id, byHandle?.Id);
        Assert.Null(await queries.FindByInvitationHandleAsync(InvitationSecret.Create().Handle));

        var awaiting = await queries.ListWithInvitationForAsync(TestEmailHasher.Instance.Hash("chiara@example.test"));
        Assert.Contains(circle.Id, awaiting.Select(c => c.Id));
        Assert.Empty(await queries.ListWithInvitationForAsync(TestEmailHasher.Instance.Hash("nobody@example.test")));
    }

    [Fact]
    public async Task PurgeAccount_RemovesTheMembershipAndTheirInvitations_ButKeepsTheirManagedMembers()
    {
        // FR-015's second seam, and FR-019's "a managed member survives their creator". An admin is
        // deleted; the people they added stay, because they are circle data rather than the admin's.
        var owner = Guid.CreateVersion7();
        var admin = Guid.CreateVersion7();

        var circle = CircleFactory.WithOwner(owner);
        var adminMembership = CircleFactory.Join(circle, owner, admin, "beat@example.test", "Beat");
        Assert.True(circle.ChangeRole(owner, adminMembership.Id, CircleRole.Admin).IsSuccess);
        var managed = CircleFactory.AddManaged(circle, admin, "Grosi");
        CircleFactory.Invite(circle, admin, "chiara@example.test");
        CircleFactory.Invite(circle, owner, "dora@example.test");

        await using var provider = fixture.CreateProvider();
        var repository = Repository(provider);
        await repository.AddAsync(circle);

        await using (var purging = fixture.CreateProvider())
        {
            var purge = Repository(purging);
            var loaded = await purge.FindAsync(circle.Id);
            loaded!.PurgeAccount(admin);
            await purge.SaveAsync(loaded);
        }

        await using var verifying = fixture.CreateProvider();
        var reloaded = await Repository(verifying).FindAsync(circle.Id);

        Assert.NotNull(reloaded);
        Assert.Null(reloaded.FindMembershipOf(admin));
        Assert.NotNull(reloaded.FindMembership(managed.Id));

        var remaining = Assert.Single(reloaded.Invitations);
        Assert.Equal(owner, remaining.InvitedByUserId);
    }

    /// <summary>
    /// The repository belonging to one provider — and therefore to one <see cref="CirclesDbContext"/>
    /// and one change tracker. Two providers are two concurrent callers.
    /// </summary>
    private static ICircleRepository Repository(ServiceProvider provider) =>
        provider.GetRequiredService<ICircleRepository>();

    /// <summary>
    /// A context that records the SQL it sends, for the one test that has to know whether a
    /// statement was issued at all rather than only what the database ended up holding.
    /// </summary>
    private CirclesDbContext LoggingContext(List<string> statements) =>
        new(new DbContextOptionsBuilder<CirclesDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .LogTo(statements.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .Options);

    private static async Task<List<TEntity>> RowsFor<TEntity>(CirclesDbContext context, Guid circleId)
        where TEntity : class =>
        await context.Set<TEntity>()
            .AsNoTracking()
            .Where(e => EF.Property<Guid>(e, "CircleId") == circleId)
            .ToListAsync();
}
