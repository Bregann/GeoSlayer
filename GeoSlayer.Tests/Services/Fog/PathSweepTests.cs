using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Tests.Services.Fog;

/// <summary>
/// Pure grid-geometry tests — no database, no container.
/// </summary>
[TestFixture]
public class PathSweepTests
{
    // ── SupercoverLine ──────────────────────────────────────────────

    [Test]
    public void SupercoverLine_SameCell_ReturnsThatCellOnly()
    {
        var cells = PathSweep.SupercoverLine(new GridCell(5, 5), new GridCell(5, 5));

        Assert.That(cells, Is.EqualTo(new[] { new GridCell(5, 5) }));
    }

    [Test]
    public void SupercoverLine_HorizontalRun_ReturnsEveryCellBetween()
    {
        var cells = PathSweep.SupercoverLine(new GridCell(3, 0), new GridCell(3, 4));

        Assert.That(cells, Is.EqualTo(new[]
        {
            new GridCell(3, 0),
            new GridCell(3, 1),
            new GridCell(3, 2),
            new GridCell(3, 3),
            new GridCell(3, 4),
        }));
    }

    [Test]
    public void SupercoverLine_VerticalRun_ReturnsEveryCellBetween()
    {
        var cells = PathSweep.SupercoverLine(new GridCell(0, 7), new GridCell(3, 7));

        Assert.That(cells, Is.EqualTo(new[]
        {
            new GridCell(0, 7),
            new GridCell(1, 7),
            new GridCell(2, 7),
            new GridCell(3, 7),
        }));
    }

    [Test]
    public void SupercoverLine_PerfectDiagonal_IncludesTheClippedNeighbours()
    {
        // A plain Bresenham diagonal returns only (0,0),(1,1),(2,2) — a fog corridor
        // with holes you can see through diagonally.  Supercover fills the corners.
        var cells = PathSweep.SupercoverLine(new GridCell(0, 0), new GridCell(2, 2));

        Assert.That(cells, Does.Contain(new GridCell(0, 0)));
        Assert.That(cells, Does.Contain(new GridCell(1, 1)));
        Assert.That(cells, Does.Contain(new GridCell(2, 2)));
        Assert.That(cells.Count, Is.GreaterThan(3), "supercover must include clipped corner cells");
    }

    [Test]
    public void SupercoverLine_AnyLine_RevealsAFourConnectedRegion()
    {
        // The property that actually matters for fog: the revealed cells form one
        // orthogonally-connected blob, so a walk leaves no pinhole you can see through.
        for (var lat = -6; lat <= 6; lat++)
        for (var lng = -6; lng <= 6; lng++)
        {
            var cells = PathSweep.SupercoverLine(new GridCell(0, 0), new GridCell(lat, lng));

            Assert.That(IsFourConnected(cells), Is.True,
                $"line to ({lat},{lng}) left a gap");
        }
    }

    /// <summary>Flood-fill the cell set orthogonally; true when it is all one region.</summary>
    private static bool IsFourConnected(IReadOnlyCollection<GridCell> cells)
    {
        var remaining = cells.ToHashSet();
        var queue = new Queue<GridCell>();

        queue.Enqueue(cells.First());
        remaining.Remove(cells.First());

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();

            foreach (var neighbour in new[]
            {
                new GridCell(cell.GridLat + 1, cell.GridLng),
                new GridCell(cell.GridLat - 1, cell.GridLng),
                new GridCell(cell.GridLat, cell.GridLng + 1),
                new GridCell(cell.GridLat, cell.GridLng - 1),
            })
            {
                if (remaining.Remove(neighbour))
                    queue.Enqueue(neighbour);
            }
        }

        return remaining.Count == 0;
    }

    [Test]
    public void SupercoverLine_ReversedEndpoints_CoversTheSameCells()
    {
        // Walking a street east-to-west must reveal exactly what walking it
        // west-to-east does.  Naive Bresenham fails this at corner crossings.
        for (var lat = -6; lat <= 6; lat++)
        for (var lng = -6; lng <= 6; lng++)
        {
            var forward = PathSweep.SupercoverLine(new GridCell(0, 0), new GridCell(lat, lng));
            var backward = PathSweep.SupercoverLine(new GridCell(lat, lng), new GridCell(0, 0));

            Assert.That(forward.ToHashSet(), Is.EquivalentTo(backward.ToHashSet()),
                $"line to ({lat},{lng}) is direction-dependent");
        }
    }

    [Test]
    public void SupercoverLine_StartsAndEndsAtTheGivenCells()
    {
        var cells = PathSweep.SupercoverLine(new GridCell(2, 9), new GridCell(-3, 1));

        Assert.That(cells.First(), Is.EqualTo(new GridCell(2, 9)));
        Assert.That(cells.Last(), Is.EqualTo(new GridCell(-3, 1)));
    }

    // ── SweepPath ───────────────────────────────────────────────────

    [Test]
    public void SweepPath_EmptyPath_RevealsNothing()
    {
        Assert.That(PathSweep.SweepPath([]), Is.Empty);
    }

    [Test]
    public void SweepPath_SinglePoint_RevealsThatCell()
    {
        var swept = PathSweep.SweepPath([new GridCell(4, 4)]);

        Assert.That(swept, Is.EquivalentTo(new[] { new GridCell(4, 4) }));
    }

    [Test]
    public void SweepPath_MultipleLegs_JoinsThemWithoutGaps()
    {
        // An L-shaped walk: east 3, then north 2.
        var swept = PathSweep.SweepPath([
            new GridCell(0, 0),
            new GridCell(0, 3),
            new GridCell(2, 3),
        ]);

        Assert.That(swept, Is.EquivalentTo(new[]
        {
            new GridCell(0, 0),
            new GridCell(0, 1),
            new GridCell(0, 2),
            new GridCell(0, 3),
            new GridCell(1, 3),
            new GridCell(2, 3),
        }));
    }

    // ── Dilate ──────────────────────────────────────────────────────

    [Test]
    public void Dilate_RadiusZero_ReturnsTheInput()
    {
        var cells = new[] { new GridCell(1, 1), new GridCell(1, 2) };

        Assert.That(PathSweep.Dilate(cells, 0), Is.EquivalentTo(cells));
    }

    [Test]
    public void Dilate_RadiusOne_ReturnsTheThreeByThreeBlock()
    {
        var dilated = PathSweep.Dilate([new GridCell(0, 0)], 1);

        Assert.That(dilated, Has.Count.EqualTo(9));
        Assert.That(dilated, Does.Contain(new GridCell(-1, -1)));
        Assert.That(dilated, Does.Contain(new GridCell(1, 1)));
    }

    [Test]
    public void Dilate_OverlappingBlocks_Deduplicates()
    {
        // Two adjacent cells dilated by 1 give a 3x4 block, not 18 cells.
        var dilated = PathSweep.Dilate([new GridCell(0, 0), new GridCell(0, 1)], 1);

        Assert.That(dilated, Has.Count.EqualTo(12));
    }
}
