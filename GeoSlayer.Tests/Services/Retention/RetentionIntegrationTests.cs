using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Idle;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Retention;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Tests.Services.Retention;

/// <summary>
/// Stage 14 criteria 2–10, against a real database.
/// </summary>
[TestFixture]
public class RetentionIntegrationTests : DatabaseIntegrationTestBase
{
    private const double OriginLat = 51.5074;
    private const double OriginLng = -0.1278;

    private RetentionService _sut = null!;
    private ProgressionService _progression = null!;
    private MaterialService _materials = null!;
    private Player _player = null!;

    private static CancellationToken Ct => CancellationToken.None;

    protected override async Task CustomSetUp()
    {
        var (_, player) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
        _player = player;

        await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);
        await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
        await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);
        await TestDatabaseSeedHelper.SeedDistrictDefinitions(DbContext);

        _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
        _materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);

        await _progression.EnsureStartingUnlocks(_player.Id, Ct);

        _sut = TestDatabaseSeedHelper.CreateRetentionService(DbContext, _materials, _progression);
    }

    private static List<GridCell> Cells(int count, int latOffset = 0) =>
        Enumerable.Range(0, count)
            .Select(i => new GridCell(1000 + latOffset + i, 2000))
            .ToList();

    // ── Criterion 6: fast movement banks and reveals nothing ────────

    [Test]
    public async Task MovementAtFifteenMetresPerSecond_BanksAndRevealsNothing()
    {
        var banked = await _sut.BankTransit(_player.Id, Cells(10), 15.0, Ct);

        var revealed = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == _player.Id);

        Assert.Multiple(() =>
        {
            Assert.That(banked, Is.EqualTo(10));
            Assert.That(revealed, Is.Zero, "transit must not reveal");
            Assert.That(TransitGrading.RevealFraction(15.0), Is.Zero);
        });
    }

    [Test]
    public async Task WalkingPace_BanksNothing()
    {
        // At walking pace the cells simply revealed — there is nothing to bank.
        var banked = await _sut.BankTransit(_player.Id, Cells(10), 1.4, Ct);

        Assert.That(banked, Is.Zero);
    }

    [Test]
    public async Task CyclingPace_BanksPartially()
    {
        // §7.1 handles cyclists "without a special case" — they bank the fraction they
        // did not reveal, rather than being rejected or fully banked.
        var banked = await _sut.BankTransit(_player.Id, Cells(10), 5.0, Ct);

        var weights = await DbContext.BankedTransits
            .Where(t => t.PlayerId == _player.Id)
            .Select(t => t.Weight)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(banked, Is.EqualTo(10));
            Assert.That(weights.All(w => w > 0 && w < 1), Is.True,
                "a cyclist's cells bank partially, not wholly");
        });
    }

    [Test]
    public async Task AlreadyRevealedGround_IsNotBanked()
    {
        // "Places you passed but did not see" — ground already walked is neither.
        var now = DateTime.UtcNow;

        foreach (var cell in Cells(5))
        {
            DbContext.RevealedCells.Add(new RevealedCell
            {
                PlayerId = _player.Id, GridLat = cell.GridLat, GridLng = cell.GridLng,
                RevealedAtUtc = now,
            });
        }

        await DbContext.SaveChangesAsync();

        var banked = await _sut.BankTransit(_player.Id, Cells(10), 15.0, Ct);

        Assert.That(banked, Is.EqualTo(5), "only the unseen half should bank");
    }

    [Test]
    public async Task TheDailyCap_StopsAFlightBankingAContinent()
    {
        var banked = await _sut.BankTransit(
            _player.Id, Cells(TransitGrading.DailyTransitCap + 200), 15.0, Ct);

        Assert.That(banked, Is.EqualTo(TransitGrading.DailyTransitCap));
    }

    [Test]
    public async Task TheSameCell_IsNotBankedTwice()
    {
        await _sut.BankTransit(_player.Id, Cells(10), 15.0, Ct);
        var second = await _sut.BankTransit(_player.Id, Cells(10), 15.0, Ct);

        Assert.That(second, Is.Zero, "passing the same spot twice is one banked cell");
    }

    // ── Criterion 7: walking redeems banked transit ─────────────────

    [Test]
    public async Task WalkingNearBankedTransit_RedeemsIt()
    {
        await _sut.BankTransit(_player.Id, Cells(9), 15.0, Ct);

        // Walk three cells in the same area.
        var result = await _sut.RedeemTransit(_player.Id, Cells(3), Ct);

        Assert.Multiple(() =>
        {
            Assert.That(result.Redeemed, Is.GreaterThan(0));
            Assert.That(result.CellsRevealed, Is.GreaterThan(0),
                "redeemed transit should actually reveal");
        });
    }

    [Test]
    public async Task RedemptionHonoursTheConfiguredRatio()
    {
        await _sut.BankTransit(_player.Id, Cells(30), 15.0, Ct);

        var result = await _sut.RedeemTransit(_player.Id, Cells(2), Ct);

        // Two walked cells redeem at most 2 × the ratio.
        Assert.That(result.Redeemed,
            Is.LessThanOrEqualTo(2 * TransitGrading.RedeemedPerWalkedCell));
    }

    [Test]
    public async Task WalkingFarFromBankedTransit_RedeemsNothing()
    {
        await _sut.BankTransit(_player.Id, Cells(10), 15.0, Ct);

        // A thousand cells away.
        var result = await _sut.RedeemTransit(_player.Id, Cells(3, latOffset: 1000), Ct);

        Assert.That(result.Redeemed, Is.Zero, "redemption is local, by design");
    }

    // ── Criterion 8: transit decays ─────────────────────────────────

    [Test]
    public async Task BankedTransit_ExpiresAfterTheWindow()
    {
        await _sut.BankTransit(_player.Id, Cells(10), 15.0, Ct);

        // Age it past the decay window.
        var rows = await DbContext.BankedTransits
            .Where(t => t.PlayerId == _player.Id)
            .ToListAsync();

        foreach (var row in rows)
            row.BankedUtc = DateTime.UtcNow - TransitGrading.DecayWindow - TimeSpan.FromHours(1);

        await DbContext.SaveChangesAsync();

        var result = await _sut.RedeemTransit(_player.Id, Cells(3), Ct);

        Assert.Multiple(() =>
        {
            Assert.That(result.Expired, Is.EqualTo(10));
            Assert.That(result.Redeemed, Is.Zero, "expired transit cannot be redeemed");
        });
    }

    [Test]
    public async Task ExpiredTransit_IsNotListed()
    {
        await _sut.BankTransit(_player.Id, Cells(10), 15.0, Ct);

        var rows = await DbContext.BankedTransits.Where(t => t.PlayerId == _player.Id).ToListAsync();

        foreach (var row in rows)
            row.BankedUtc = DateTime.UtcNow - TransitGrading.DecayWindow - TimeSpan.FromHours(1);

        await DbContext.SaveChangesAsync();

        var listed = await _sut.GetBankedTransit(_player.Id, Ct);

        Assert.That(listed, Is.Empty);
    }

    // ── Criteria 2 & 3: expeditions ─────────────────────────────────

    private async Task<(Worker Worker, PointOfInterest Poi)> SetUpExpedition(bool withVisit = true)
    {
        var poi = new PointOfInterest
        {
            OsmId = Random.Shared.NextInt64(1, long.MaxValue),
            OsmType = "node",
            Name = "Distant Cathedral",
            Skill = SkillType.Prayer,
            Location = new Point(OriginLng + 0.5, OriginLat + 0.5) { SRID = 4326 },
            XpReward = 25,
        };

        DbContext.PointsOfInterest.Add(poi);

        var worker = new Worker
        {
            PlayerId = _player.Id,
            Name = "Worker 1",
            Tier = 1,
            StartedAtUtc = DateTime.UtcNow,
            LastCollectedAtUtc = DateTime.UtcNow,
        };

        DbContext.Workers.Add(worker);
        await DbContext.SaveChangesAsync();

        if (withVisit)
        {
            DbContext.PlayerPoiVisits.Add(new PlayerPoiVisit
            {
                PlayerId = _player.Id,
                PoiId = poi.Id,
                VisitCount = 1,
                TotalVisits = 1,
                FirstVisitUtc = DateTime.UtcNow.AddDays(-30),
                LastVisitUtc = DateTime.UtcNow.AddDays(-30),
            });

            await DbContext.SaveChangesAsync();
        }

        var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
        player.LastLatitude = OriginLat;
        player.LastLongitude = OriginLng;
        player.LastSyncAtUtc = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();

        return (worker, poi);
    }

    [Test]
    public async Task AWorkerCanBeSent_OnlyToAPoiWithARealPriorVisit()
    {
        var (worker, poi) = await SetUpExpedition();

        var expedition = await _sut.DispatchExpedition(_player.Id, worker.Id, poi.Id, Ct);

        Assert.That(expedition.PoiName, Is.EqualTo("Distant Cathedral"));
    }

    [Test]
    public async Task AWorkerCannotBeSent_ToAPoiNeverVisited()
    {
        // This is what turns "every trip the player has ever taken" into the asset —
        // without the visit requirement it would just be a map browser.
        var (worker, poi) = await SetUpExpedition(withVisit: false);

        await Assert.ThatAsync(
            () => _sut.DispatchExpedition(_player.Id, worker.Id, poi.Id, Ct),
            Throws.TypeOf<BadRequestException>());
    }

    [Test]
    public async Task ExpeditionDuration_ScalesWithDistance()
    {
        var near = ExpeditionMath.Duration(1_000);
        var far = ExpeditionMath.Duration(100_000);

        Assert.Multiple(() =>
        {
            Assert.That(far, Is.GreaterThan(near));
            Assert.That(near.TotalHours, Is.GreaterThanOrEqualTo(ExpeditionMath.MinimumHours));
            Assert.That(far.TotalHours, Is.LessThanOrEqualTo(ExpeditionMath.MaximumHours),
                "a distant POI is a long trip, not an abandoned worker");
        });

        await Task.CompletedTask;
    }

    [Test]
    public async Task ADispatchedWorker_LeavesItsClaim()
    {
        // Occupies the worker, competing with Claim work — a real decision (§5.4).
        var (worker, poi) = await SetUpExpedition();

        await _sut.DispatchExpedition(_player.Id, worker.Id, poi.Id, Ct);

        var after = await DbContext.Workers.FirstAsync(w => w.Id == worker.Id);

        Assert.That(after.ClaimId, Is.Null);
    }

    [Test]
    public async Task AWorkerAlreadyAway_CannotBeSentAgain()
    {
        var (worker, poi) = await SetUpExpedition();

        await _sut.DispatchExpedition(_player.Id, worker.Id, poi.Id, Ct);

        await Assert.ThatAsync(
            () => _sut.DispatchExpedition(_player.Id, worker.Id, poi.Id, Ct),
            Throws.TypeOf<BadRequestException>());
    }

    [Test]
    public async Task AReturnedExpedition_YieldsThePoisMaterials()
    {
        var (worker, poi) = await SetUpExpedition();

        // Unlock Prayer so the XP has somewhere to land.
        DbContext.PlayerSkills.Add(new PlayerSkill
        {
            PlayerId = _player.Id, SkillType = SkillType.Prayer, Level = 1,
            UnlockedAtUtc = DateTime.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        var expedition = await _sut.DispatchExpedition(_player.Id, worker.Id, poi.Id, Ct);

        var row = await DbContext.WorkerExpeditions.FirstAsync(e => e.Id == expedition.Id);
        row.ReturnsUtc = DateTime.UtcNow.AddSeconds(-1);
        await DbContext.SaveChangesAsync();

        var collected = await _sut.CollectExpeditions(_player.Id, Ct);

        Assert.Multiple(() =>
        {
            Assert.That(collected.HasCollection, Is.True);
            Assert.That(collected.Returned, Contains.Item("Distant Cathedral"));
            Assert.That(collected.SkillXpEarned, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task AnExpeditionStillAway_IsNotCollected()
    {
        var (worker, poi) = await SetUpExpedition();

        await _sut.DispatchExpedition(_player.Id, worker.Id, poi.Id, Ct);

        var collected = await _sut.CollectExpeditions(_player.Id, Ct);

        Assert.That(collected.HasCollection, Is.False);
    }

    // ── Destinations: the visit log as a menu ───────────────────────

    [Test]
    public async Task DestinationsList_OnlyPoisActuallyVisited()
    {
        var (_, poi) = await SetUpExpedition();

        // A second POI the player has never been to.
        DbContext.PointsOfInterest.Add(new PointOfInterest
        {
            OsmId = Random.Shared.NextInt64(1, long.MaxValue),
            OsmType = "node",
            Name = "Never Visited",
            Skill = SkillType.Knowledge,
            Location = new Point(OriginLng + 0.2, OriginLat + 0.2) { SRID = 4326 },
            XpReward = 10,
        });
        await DbContext.SaveChangesAsync();

        var destinations = await _sut.GetExpeditionDestinations(_player.Id, Ct);

        Assert.Multiple(() =>
        {
            Assert.That(destinations.Select(d => d.PoiId), Contains.Item(poi.Id));
            Assert.That(destinations.Any(d => d.Name == "Never Visited"), Is.False,
                "the menu is the visit log, not the POI table");
        });
    }

    [Test]
    public async Task DestinationsCarryTheTradeOff()
    {
        // Distance, duration and estimated yield shown before dispatching, so choosing is
        // a decision rather than a guess.
        await SetUpExpedition();

        var destination = (await _sut.GetExpeditionDestinations(_player.Id, Ct)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(destination.DistanceMetres, Is.GreaterThan(0));
            Assert.That(destination.DurationHours, Is.GreaterThanOrEqualTo(ExpeditionMath.MinimumHours));
            Assert.That(destination.EstimatedMaterials, Is.GreaterThan(0));
            Assert.That(destination.FirstVisitUtc, Is.LessThan(DateTime.UtcNow));
        });
    }

    [Test]
    public async Task ADestinationWithAWorkerAlreadyThere_IsMarkedUnavailable()
    {
        var (worker, poi) = await SetUpExpedition();

        var before = (await _sut.GetExpeditionDestinations(_player.Id, Ct)).Single();
        Assert.That(before.IsAvailable, Is.True);

        await _sut.DispatchExpedition(_player.Id, worker.Id, poi.Id, Ct);

        var after = (await _sut.GetExpeditionDestinations(_player.Id, Ct)).Single();

        Assert.That(after.IsAvailable, Is.False);
    }

    [Test]
    public async Task DestinationsAreOrderedFurthestFirst()
    {
        // The distant POI is the interesting choice — the whole point is that an old
        // holiday trip still pays.
        await SetUpExpedition();

        var near = new PointOfInterest
        {
            OsmId = Random.Shared.NextInt64(1, long.MaxValue),
            OsmType = "node",
            Name = "Corner Shop",
            Skill = SkillType.Trading,
            Location = new Point(OriginLng + 0.001, OriginLat + 0.001) { SRID = 4326 },
            XpReward = 10,
        };

        DbContext.PointsOfInterest.Add(near);
        await DbContext.SaveChangesAsync();

        DbContext.PlayerPoiVisits.Add(new PlayerPoiVisit
        {
            PlayerId = _player.Id, PoiId = near.Id, VisitCount = 1, TotalVisits = 1,
            FirstVisitUtc = DateTime.UtcNow, LastVisitUtc = DateTime.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        var destinations = await _sut.GetExpeditionDestinations(_player.Id, Ct);

        Assert.That(destinations[0].Name, Is.EqualTo("Distant Cathedral"));
    }

    [Test]
    public async Task WithNoVisits_TheMenuIsEmpty()
    {
        var destinations = await _sut.GetExpeditionDestinations(_player.Id, Ct);

        Assert.That(destinations, Is.Empty);
    }

    // ── Criteria 4 & 5: patrol routes ───────────────────────────────

    private async Task<int> CreateRoute()
    {
        var route = await _sut.CreatePatrolRoute(
            _player.Id,
            "Morning Loop",
            [(OriginLat, OriginLng), (OriginLat + 0.002, OriginLng), (OriginLat + 0.002, OriginLng + 0.002)],
            Ct);

        return route.Id;
    }

    [Test]
    public async Task ACircuitCompletes_WhenWaypointsAreHitInOrder()
    {
        await CreateRoute();

        var completions = await _sut.CheckPatrolCompletion(
            _player.Id,
            [(OriginLat, OriginLng), (OriginLat + 0.002, OriginLng), (OriginLat + 0.002, OriginLng + 0.002)],
            Ct);

        Assert.That(completions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ACircuitDoesNotComplete_WhenWaypointsAreOutOfOrder()
    {
        // Order is the point: accepting them in any order would let a player who lives
        // inside the loop claim it without walking it.
        await CreateRoute();

        var completions = await _sut.CheckPatrolCompletion(
            _player.Id,
            [(OriginLat + 0.002, OriginLng + 0.002), (OriginLat + 0.002, OriginLng), (OriginLat, OriginLng)],
            Ct);

        Assert.That(completions, Is.Empty);
    }

    [Test]
    public async Task APartialCircuit_DoesNotComplete()
    {
        await CreateRoute();

        var completions = await _sut.CheckPatrolCompletion(
            _player.Id,
            [(OriginLat, OriginLng), (OriginLat + 0.002, OriginLng)],
            Ct);

        Assert.That(completions, Is.Empty);
    }

    [Test]
    public async Task PatrolCompletion_AwardsUpkeepAndNoXp()
    {
        // §5.7: novelty stays the only route to progress; routine becomes the route to
        // maintenance. XP here would collapse that distinction.
        await TestDatabaseSeedHelper.SeedCraftingDefinitions(DbContext);
        await CreateRoute();

        var xpBefore = await DbContext.Players
            .Where(p => p.Id == _player.Id).Select(p => p.AdventurerXp).FirstAsync();

        var completions = await _sut.CheckPatrolCompletion(
            _player.Id,
            [(OriginLat, OriginLng), (OriginLat + 0.002, OriginLng), (OriginLat + 0.002, OriginLng + 0.002)],
            Ct);

        var xpAfter = await DbContext.Players
            .Where(p => p.Id == _player.Id).Select(p => p.AdventurerXp).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(completions[0].UpkeepAwarded, Is.GreaterThan(0));
            Assert.That(xpAfter, Is.EqualTo(xpBefore), "re-walking is not new ground");
        });
    }

    [Test]
    public async Task ACircuitWalkedTwiceInADay_PaysOnce()
    {
        await CreateRoute();

        var path = new List<(double, double)>
        {
            (OriginLat, OriginLng),
            (OriginLat + 0.002, OriginLng),
            (OriginLat + 0.002, OriginLng + 0.002),
        };

        await _sut.CheckPatrolCompletion(_player.Id, path, Ct);
        var second = await _sut.CheckPatrolCompletion(_player.Id, path, Ct);

        Assert.That(second, Is.Empty, "the reward is maintenance, not a grind target");
    }

    [Test]
    public async Task APatrolNeedsAtLeastTwoWaypoints()
    {
        await Assert.ThatAsync(
            () => _sut.CreatePatrolRoute(_player.Id, "Too Short", [(OriginLat, OriginLng)], Ct),
            Throws.TypeOf<BadRequestException>());
    }

    // ── Criterion 9: districts need variety ─────────────────────────

    private async Task<Claim> AddClaim(int lat, int lng, TerrainType terrain)
    {
        var claim = new Claim
        {
            PlayerId = _player.Id,
            CentreGridLat = lat,
            CentreGridLng = lng,
            Size = 3,
            TerrainProfile = terrain,
            Name = $"Claim {lat},{lng}",
            ClaimedUtc = DateTime.UtcNow,
        };

        DbContext.Claims.Add(claim);
        await DbContext.SaveChangesAsync();

        return claim;
    }

    [Test]
    public async Task AVariedDistrict_GrantsItsBonus()
    {
        await AddClaim(100, 100, TerrainType.Woodland);
        await AddClaim(103, 100, TerrainType.Water);

        var status = await _sut.GetDistrictStatus(_player.Id, Ct);

        Assert.Multiple(() =>
        {
            Assert.That(status.DistrictKey, Is.Not.Null);
            Assert.That(status.OutputBonus, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task AUniformDistrict_GrantsNothing()
    {
        // Without this rule the optimal play is nine identical cells of your best terrain
        // (§5.5), and the map stops mattering.
        await AddClaim(200, 200, TerrainType.Woodland);
        await AddClaim(203, 200, TerrainType.Woodland);
        await AddClaim(206, 200, TerrainType.Woodland);

        var status = await _sut.GetDistrictStatus(_player.Id, Ct);

        Assert.Multiple(() =>
        {
            Assert.That(status.DistrictKey, Is.Null);
            Assert.That(status.OutputBonus, Is.Zero);
        });
    }

    [Test]
    public async Task NonContiguousVariedClaims_GrantNothing()
    {
        // Variety alone is not enough — the Claims must touch, or a scattered holding
        // would count as a District.
        await AddClaim(300, 300, TerrainType.Woodland);
        await AddClaim(900, 900, TerrainType.Water);

        var status = await _sut.GetDistrictStatus(_player.Id, Ct);

        Assert.That(status.DistrictKey, Is.Null);
    }

    [Test]
    public async Task ARicherDistrict_OutranksAPoorerOne()
    {
        await AddClaim(400, 400, TerrainType.Woodland);
        await AddClaim(403, 400, TerrainType.Water);
        await AddClaim(406, 400, TerrainType.Farmland);

        var status = await _sut.GetDistrictStatus(_player.Id, Ct);

        // Homestead (woodland+water+farmland, 0.15) beats Riverside (woodland+water, 0.08).
        Assert.That(status.DistrictKey, Is.EqualTo("homestead"));
    }

    // ── Criterion 10: surges boost and expire ───────────────────────

    [Test]
    public async Task ASurgeIsGenerated_AndIsActive()
    {
        var surge = await _sut.EnsureSurgeFor(OriginLat, OriginLng, Ct);

        Assert.That(surge, Is.Not.Null);

        var active = await _sut.GetActiveSurges(OriginLat, OriginLng, Ct);

        Assert.Multiple(() =>
        {
            Assert.That(active, Has.Count.EqualTo(1));
            Assert.That(active[0].Multiplier, Is.GreaterThan(1), "a surge must actually boost");
            Assert.That(active[0].EndsUtc, Is.GreaterThan(DateTime.UtcNow));
        });
    }

    [Test]
    public async Task ASecondSurge_IsNotCreatedWhileOneIsActive()
    {
        await _sut.EnsureSurgeFor(OriginLat, OriginLng, Ct);
        var second = await _sut.EnsureSurgeFor(OriginLat, OriginLng, Ct);

        Assert.That(second, Is.Null);
    }

    [Test]
    public async Task AnExpiredSurge_IsNotReturned()
    {
        await _sut.EnsureSurgeFor(OriginLat, OriginLng, Ct);

        var surge = await DbContext.ResourceSurges.FirstAsync();
        surge.EndsUtc = DateTime.UtcNow.AddMinutes(-1);
        await DbContext.SaveChangesAsync();

        var active = await _sut.GetActiveSurges(OriginLat, OriginLng, Ct);

        Assert.That(active, Is.Empty, "a surge must expire on schedule");
    }

    [Test]
    public async Task ASurgeIsModest_SoIgnoringItIsViable()
    {
        // §5.6: "a player who ignores every surge must still progress fine". A large
        // multiplier would make chasing them mandatory.
        await _sut.EnsureSurgeFor(OriginLat, OriginLng, Ct);

        var active = await _sut.GetActiveSurges(OriginLat, OriginLng, Ct);

        Assert.That(active[0].Multiplier, Is.LessThanOrEqualTo(2.0),
            "a nudge, not an obligation");
    }

    [Test]
    public async Task SurgesAreSharedAcrossPlayers()
    {
        // A surge is a property of a place, so two players in the same region should see
        // the same one rather than each rolling their own.
        await _sut.EnsureSurgeFor(OriginLat, OriginLng, Ct);

        var otherUser = await TestDatabaseSeedHelper.SeedTestUser(DbContext, "surge_neighbour");
        await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, otherUser);

        var theirs = await _sut.GetActiveSurges(OriginLat, OriginLng, Ct);

        Assert.That(theirs, Has.Count.EqualTo(1));
    }
}
