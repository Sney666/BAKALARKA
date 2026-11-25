using System.Collections.Generic;
using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public static class GridRayHelper
    {
        public static bool IsValidDirection(TargetingType targeting, GridPos direction)
        {
            return targeting switch
            {
                TargetingType.Orthogonal => direction.IsOrthogonalDirection(),
                TargetingType.Diagonal => direction.IsDiagonalDirection(),
                TargetingType.Self => direction.X == 0 && direction.Y == 0,
                _ => false
            };
        }
        public static List<GridPos> TraceRayTiles(
        ActiveRunState run,
        GridPos origin,
        GridPos direction,
        int maxRange,
        int maxEnemyHits,
        out List<EnemyInstance> hitEnemies)
        {
            List<GridPos> tiles = new List<GridPos>();
            hitEnemies = new List<EnemyInstance>();

            for (int step = 1; step <= maxRange; step++)
            {
                GridPos current = origin.Offset(direction.X * step, direction.Y * step);
                if (!current.IsValid())
                {
                    break;
                }

                tiles.Add(current);
                EnemyInstance? enemy = run.GetEnemyAt(current);
                if (enemy == null)
                {
                    continue;
                }

                hitEnemies.Add(enemy);
                if (hitEnemies.Count >= maxEnemyHits)
                {
                    break;
                }
            }

            return tiles;
        }

        public static List<GridPos> GetArcSweepTiles(GridPos origin, GridPos direction)
        {
            List<GridPos> tiles = new List<GridPos>();
            GridPos center = origin.Offset(direction);
            GridPos left = new GridPos(center.X - direction.Y, center.Y + direction.X);
            GridPos right = new GridPos(center.X + direction.Y, center.Y - direction.X);

            if (center.IsValid())
            {
                tiles.Add(center);
            }

            if (left.IsValid())
            {
                tiles.Add(left);
            }

            if (right.IsValid())
            {
                tiles.Add(right);
            }

            return tiles;
        }
    }
}