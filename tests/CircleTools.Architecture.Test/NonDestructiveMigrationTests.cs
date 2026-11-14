// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Production holds real data from 2026-10-25 on (ADR-0087): no migration may drop or delete it. Every migration of
/// every context is built and its operations inspected; one that drops a table, a column or a schema, deletes rows,
/// or runs SQL that does, fails this test — unless it is listed below. The list holds the migrations from before
/// that date; adding to it needs a new ADR that says why the data may go and how it is saved first.
/// </summary>
public sealed partial class NonDestructiveMigrationTests
{
    // The destructive migrations that ran while production held test data only. Never add to this list without an ADR.
    private static readonly HashSet<string> BeforeRealData = new(StringComparer.Ordinal)
    {
        "20260728131459_DropUnusedPhoneColumns", // Identity columns never used
        "20260901175155_DropHeartbeatsTable", // the walking-skeleton sample
        "20261025065336_RetireOldGroupStores", // ADR-0086: the linking and shared stores, test data only (D2)
        "20261025065346_DropGroupSync", // ADR-0086: group copies in the account sync, test data only (D2)
    };

    private static readonly Assembly[] MigrationAssemblies =
    [
        typeof(CircleTools.Main.Service.Repository.MainDbContext).Assembly,
        typeof(CircleTools.Circles.Repository.CirclesDbContext).Assembly,
        typeof(CircleTools.SecretSanta.Service.Repository.SecretSantaDbContext).Assembly,
    ];

    [Fact]
    public void NoMigration_DropsOrDeletesData_ExceptTheOnesFromBeforeRealData()
    {
        var destructive = Migrations()
            .Select(m => (m.Id, Reasons: DestructiveOperations(m.Migration).ToList()))
            .Where(m => m.Reasons.Count > 0 && !BeforeRealData.Contains(m.Id))
            .Select(m => $"{m.Id}: {string.Join("; ", m.Reasons)}")
            .ToList();

        Assert.True(destructive.Count == 0,
            "These migrations drop or delete data, which production no longer allows (ADR-0087):\n" + string.Join("\n", destructive));
    }

    [Fact]
    public void TheMigrationsAreFound() =>
        Assert.Contains(Migrations(), m => m.Id.EndsWith("_InitialGroupStore", StringComparison.Ordinal));

    private static IEnumerable<(string Id, Migration Migration)> Migrations() =>
        MigrationAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract && t.GetCustomAttribute<MigrationAttribute>() is not null)
            .Select(t => (t.GetCustomAttribute<MigrationAttribute>()!.Id, (Migration)Activator.CreateInstance(t)!));

    private static IEnumerable<string> DestructiveOperations(Migration migration)
    {
        foreach (var operation in migration.UpOperations)
        {
            switch (operation)
            {
                case DropTableOperation drop:
                    yield return $"drops table {drop.Schema}.{drop.Name}";
                    break;
                case DropColumnOperation drop:
                    yield return $"drops column {drop.Schema}.{drop.Table}.{drop.Name}";
                    break;
                case DropSchemaOperation drop:
                    yield return $"drops schema {drop.Name}";
                    break;
                case DeleteDataOperation delete:
                    yield return $"deletes rows from {delete.Schema}.{delete.Table}";
                    break;
                case SqlOperation sql when DestructiveSql().IsMatch(sql.Sql):
                    yield return $"runs destructive SQL: {sql.Sql.Trim()}";
                    break;
            }
        }
    }

    [GeneratedRegex(@"\b(DROP|DELETE|TRUNCATE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DestructiveSql();
}
