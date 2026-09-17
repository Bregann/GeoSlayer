namespace GeoSlayer.Domain.Services.Fog
{
    /// <summary>
    /// A single grid cell, identified by its integer grid coordinates.
    /// </summary>
    public readonly record struct GridCell(int GridLat, int GridLng);

    /// <summary>
    /// Pure grid geometry for turning a walked path into the set of cells it crossed.
    ///
    /// Kept free of EF, DI and clocks so it can be unit tested directly — the reveal
    /// correctness of the whole game rests on this being right.
    /// </summary>
    public static class PathSweep
    {
        /// <summary>
        /// Every cell touched by the straight line between two cells, endpoints included.
        ///
        /// This is a <i>supercover</i> line: unlike plain Bresenham it also returns the cells a
        /// diagonal crossing merely clips, so the returned set is contiguous in the
        /// 4-neighbour sense and a walk leaves no diagonal gaps in the fog.
        /// </summary>
        public static List<GridCell> SupercoverLine(GridCell from, GridCell to)
        {
            int x = from.GridLng, y = from.GridLat;

            var cells = new List<GridCell> { new(y, x) };

            var dx = to.GridLng - from.GridLng;
            var dy = to.GridLat - from.GridLat;

            var stepX = Math.Sign(dx);
            var stepY = Math.Sign(dy);

            var nx = Math.Abs(dx);
            var ny = Math.Abs(dy);

            // Walk cell-centre to cell-centre, crossing one boundary at a time.  `ix` and
            // `iy` count boundaries crossed so far.  The line reaches its ix-th vertical
            // boundary at parameter (2·ix+1)/(2·nx) along the segment and its iy-th
            // horizontal boundary at (2·iy+1)/(2·ny); cross-multiplying compares the two
            // in exact integer arithmetic, with no division and no rounding.
            int ix = 0, iy = 0;

            while (ix < nx || iy < ny)
            {
                // Remaining crossings on one axis only — finish along that axis.
                if (iy == ny)
                {
                    x += stepX;
                    ix++;
                }
                else if (ix == nx)
                {
                    y += stepY;
                    iy++;
                }
                else
                {
                    var vertical = (1 + 2 * ix) * ny;
                    var horizontal = (1 + 2 * iy) * nx;

                    if (vertical == horizontal)
                    {
                        // The line passes exactly through a corner.  Take *both* cells
                        // sharing it — taking only one would make the result depend on
                        // which end of the segment we started from.
                        cells.Add(new GridCell(y, x + stepX));
                        cells.Add(new GridCell(y + stepY, x));
                        x += stepX;
                        y += stepY;
                        ix++;
                        iy++;
                    }
                    else if (vertical < horizontal)
                    {
                        x += stepX;
                        ix++;
                    }
                    else
                    {
                        y += stepY;
                        iy++;
                    }
                }

                cells.Add(new GridCell(y, x));
            }

            return cells;
        }

        /// <summary>
        /// Every cell swept by a path of positions, in order, with no gaps between
        /// consecutive fixes.
        /// </summary>
        public static HashSet<GridCell> SweepPath(IReadOnlyList<GridCell> path)
        {
            var swept = new HashSet<GridCell>();

            if (path.Count == 0)
                return swept;

            swept.Add(path[0]);

            for (var i = 1; i < path.Count; i++)
            foreach (var cell in SupercoverLine(path[i - 1], path[i]))
                swept.Add(cell);

            return swept;
        }

        /// <summary>
        /// Expand a set of cells by <paramref name="radius"/> in each direction, giving the
        /// (2r+1)² block around every swept cell.  Radius 0 returns the input unchanged.
        /// </summary>
        public static HashSet<GridCell> Dilate(IEnumerable<GridCell> cells, int radius)
        {
            var dilated = new HashSet<GridCell>();

            foreach (var cell in cells)
            for (var dLat = -radius; dLat <= radius; dLat++)
            for (var dLng = -radius; dLng <= radius; dLng++)
                dilated.Add(new GridCell(cell.GridLat + dLat, cell.GridLng + dLng));

            return dilated;
        }
    }
}
