using System;
using System.Collections.Generic;
using System.Linq;
using RogueCards.Domain;
namespace RogueCards.Application.Enemies
{
    public static class EnemyMovementHelper
    {
        public static bool IsAlignedOrthogonally(GridPos a, GridPos b)
        {
            return a.X == b.X || a.Y == b.Y;
        }
        public static GridPos GetOrthogonalDirectionToward(GridPos from, GridPos to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return new GridPos(dx >= 0 ? 1 : -1, 0);
            }

            return new GridPos(0, dy >= 0 ? 1 : -1);
        }

        public static List<GridPos> CollectRayTiles(GridPos origin, GridPos direction, int maxRange)
        {
            List<GridPos> tiles = new List<GridPos>();
            for (int step = 1; step <= maxRange; step++)
            {
                GridPos tile = origin.Offset(direction.X * step, direction.Y * step);
                if (!tile.IsValid())
                {
                    break;
                }

                tiles.Add(tile);
            }

            return tiles;
        }

        public static bool TryStepToward(ICombatContext context, EnemyInstance enemy, GridPos target)
        {
            int currentDist = enemy.Position.ManhattanDistance(target);
            List<GridPos> candidates = GetValidAdjacentMoves(context.Run, enemy.Position)
                .Where(pos => pos.ManhattanDistance(target) < currentDist)
                .OrderBy(pos => pos.ManhattanDistance(target))
                .ThenBy(pos => IsAlignedOrthogonally(pos, target) ? 0 : 1)
                .ToList();

            if (candidates.Count == 0)
            {
                return false;
            }

            return MoveEnemy(context, enemy, candidates[0]);
        }

        public static bool TryStepAway(ICombatContext context, EnemyInstance enemy, GridPos threat)
        {
            int currentDist = enemy.Position.ManhattanDistance(threat);
            List<GridPos> candidates = GetValidAdjacentMoves(context.Run, enemy.Position)
                .Where(pos => pos.ManhattanDistance(threat) > currentDist)
                .OrderByDescending(pos => pos.ManhattanDistance(threat))
                .ThenBy(pos => IsAlignedOrthogonally(pos, threat) ? 0 : 1)
                .ToList();

            if (candidates.Count == 0)
            {
                return false;
            }

            return MoveEnemy(context, enemy, candidates[0]);
        }

        public static bool TryAlignRowOrColumn(
            ICombatContext context,
            EnemyInstance enemy,
            GridPos target,
            int minPreferredDistance = 0)
        {
            List<GridPos> candidates = GetValidAdjacentMoves(context.Run, enemy.Position);
            if (candidates.Count == 0)
            {
                return false;
            }

            GridPos best = candidates
                .OrderBy(pos => pos.ManhattanDistance(target) < minPreferredDistance ? 1 : 0)
                .ThenBy(pos => Math.Min(Math.Abs(pos.X - target.X), Math.Abs(pos.Y - target.Y)))
                .ThenBy(pos => Math.Abs(pos.ManhattanDistance(target) - Math.Max(minPreferredDistance, 1)))
                .First();

            return MoveEnemy(context, enemy, best);
        }

        public static bool StrikeFirstEntityOnRay(
            ICombatContext context,
            EnemyInstance attacker,
            GridPos direction,
            int maxRange,
            int damage,
            string attackName)
        {
            for (int step = 1; step <= maxRange; step++)
            {
                GridPos tile = attacker.Position.Offset(direction.X * step, direction.Y * step);
                if (!tile.IsValid())
                {
                    break;
                }

                if (context.Run.Player.Position == tile)
                {
                    context.DamagePlayer(attacker, damage, attackName);
                    return true;
                }

                EnemyInstance? victim = context.Run.GetEnemyAt(tile);
                if (victim != null && victim.InstanceId != attacker.InstanceId)
                {
                    context.DamageEnemyByFriendlyFire(attacker, victim, damage, attackName);
                    return true;
                }
            }

            context.Log($"{attacker.Type}'s {attackName} missed all targets.");
            return false;
        }

        public static void StrikeTiles(
            ICombatContext context,
            EnemyInstance attacker,
            IEnumerable<GridPos> tiles,
            int damage,
            string attackName)
        {
            bool hitAnything = false;

            foreach (GridPos tile in tiles)
            {
                if (context.Run.Player.Position == tile)
                {
                    context.DamagePlayer(attacker, damage, attackName);
                    hitAnything = true;
                }

                EnemyInstance? victim = context.Run.GetEnemyAt(tile);
                if (victim != null && victim.InstanceId != attacker.InstanceId)
                {
                    context.DamageEnemyByFriendlyFire(attacker, victim, damage, attackName);
                    hitAnything = true;
                }
            }

            if (!hitAnything)
            {
                context.Log($"{attacker.Type}'s {attackName} hit empty tiles.");
            }
        }

        public static void ResetAfterAttack(EnemyInstance enemy)
        {
            enemy.State = EnemyState.Armed;
            enemy.ChargeTurnsRemaining = 0;
            enemy.LockedTargetTiles.Clear();
            enemy.LockedDirection = new GridPos(0, 0);
            enemy.LockedAttackType = string.Empty;
        }

        private static List<GridPos> GetValidAdjacentMoves(ActiveRunState run, GridPos origin)
        {
            List<GridPos> moves = new List<GridPos>();
            foreach (GridPos dir in GridPos.OrthogonalDirections)
            {
                GridPos next = origin.Offset(dir);
                if (next.IsValid() && !run.IsTileOccupied(next))
                {
                    moves.Add(next);
                }
            }

            return moves;
        }

        private static bool MoveEnemy(ICombatContext context, EnemyInstance enemy, GridPos destination)
        {
            enemy.Position = destination;
            context.Log($"{enemy.Type} moved to {destination}.");
            context.TriggerHazardsForEnemy(enemy);
            return true;
        }
    }
}