#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RogueCards
{
    public class AimPreviewResult
    {
        public GridPos Direction { get; set; }
        public List<GridPos> AffectedTiles { get; set; } = new List<GridPos>();
        public List<GridPos> HitEnemyTiles { get; set; } = new List<GridPos>();
        public GridPos? MoveDestination { get; set; }
        public bool IsMovement { get; set; }
        public bool IsValidAim { get; set; }
    }

    public class CardResolver
    {
        private const int GridSize = GameEngine.GridSize;

        public AimPreviewResult ComputeAimPreview(
            ActiveRunState run,
            CardInstance card,
            GridPos direction)
        {
            AimPreviewResult result = new AimPreviewResult
            {
                Direction = direction,
                IsValidAim = false
            };

            if (run == null || run.Player == null || card == null || card.Definition == null)
            {
                return result;
            }

            WeaponDefinition def = card.Definition;
            GridPos origin = run.Player.Position;

            if (def.Targeting == TargetingType.Self)
            {
                result.IsValidAim = true;
                result.AffectedTiles.Add(origin);
                return result;
            }

            if (def.Targeting == TargetingType.Orthogonal && !IsOrthogonalDirection(direction))
            {
                return result;
            }

            if (def.Targeting == TargetingType.Diagonal && !IsDiagonalDirection(direction))
            {
                return result;
            }

            result.IsValidAim = true;
            result.IsMovement = def.Category == CardCategory.Movement;

            switch (def.Effect)
            {
                case WeaponEffectType.SingleAdjacent:
                case WeaponEffectType.KnockbackAdjacent:
                    AddTileIfValid(run, result, origin.Offset(direction.X, direction.Y));
                    break;

                case WeaponEffectType.ArcSweep:
                    GridPos[] arcTiles = GetArcSweepTiles(origin, direction);
                    foreach (GridPos tile in arcTiles)
                    {
                        AddTileIfValid(run, result, tile);
                    }
                    break;

                case WeaponEffectType.LineProjectile:
                case WeaponEffectType.DiagonalProjectile:
                    TraceLinePreview(run, result, origin, direction, GridSize, 1);
                    break;

                case WeaponEffectType.PierceTwoLine:
                    TraceLinePreview(run, result, origin, direction, GridSize, 2);
                    break;

                case WeaponEffectType.HarpoonPull:
                    TraceLinePreview(run, result, origin, direction, 3, 1);
                    break;

                case WeaponEffectType.DiagonalDash:
                    ComputeDiagonalDashPreview(run, result, origin, direction);
                    break;

                case WeaponEffectType.SlideTwo:
                    ComputeSlidePreview(run, result, origin, direction);
                    break;

                case WeaponEffectType.PlaceBearTrap:
                    result.AffectedTiles.Add(origin);
                    break;
            }

            return result;
        }

        public bool ResolveCard(
            GameEngine engine,
            CardInstance card,
            GridPos direction)
        {
            if (engine == null || engine.CurrentRun == null || card == null || card.Definition == null)
            {
                return false;
            }

            ActiveRunState run = engine.CurrentRun;
            WeaponDefinition def = card.Definition;
            int damage = engine.GetEffectiveCardDamage(card);
            GridPos origin = run.Player.Position;

            if (def.Targeting == TargetingType.Orthogonal && !IsOrthogonalDirection(direction))
            {
                return false;
            }

            if (def.Targeting == TargetingType.Diagonal && !IsDiagonalDirection(direction))
            {
                return false;
            }

            switch (def.Effect)
            {
                case WeaponEffectType.SingleAdjacent:
                    ResolveSingleAdjacent(engine, card, origin, direction, damage);
                    break;

                case WeaponEffectType.KnockbackAdjacent:
                    ResolveKnockbackAdjacent(engine, card, origin, direction, damage);
                    break;

                case WeaponEffectType.ArcSweep:
                    ResolveArcSweep(engine, card, origin, direction, damage);
                    break;

                case WeaponEffectType.LineProjectile:
                case WeaponEffectType.DiagonalProjectile:
                    ResolveLineProjectile(engine, card, origin, direction, damage, GridSize, 1);
                    break;

                case WeaponEffectType.PierceTwoLine:
                    ResolveLineProjectile(engine, card, origin, direction, damage, GridSize, 2);
                    break;

                case WeaponEffectType.PlaceBearTrap:
                    ResolvePlaceBearTrap(engine, card, origin, damage);
                    break;

                case WeaponEffectType.HarpoonPull:
                    ResolveHarpoonPull(engine, card, origin, direction, damage);
                    break;

                case WeaponEffectType.DiagonalDash:
                    ResolveDiagonalDash(engine, card, origin, direction);
                    break;

                case WeaponEffectType.SlideTwo:
                    ResolveSlideTwo(engine, card, origin, direction);
                    break;
            }

            return true;
        }

        public static bool TryGetDirectionFromHover(
            GridPos playerPos,
            GridPos hoverTile,
            TargetingType targeting,
            out GridPos direction)
        {
            direction = new GridPos(0, 0);
            int dx = hoverTile.X - playerPos.X;
            int dy = hoverTile.Y - playerPos.Y;

            if (dx == 0 && dy == 0)
            {
                return false;
            }

            if (targeting == TargetingType.Orthogonal)
            {
                if (Math.Abs(dx) >= Math.Abs(dy))
                {
                    direction = new GridPos(Math.Sign(dx), 0);
                }
                else
                {
                    direction = new GridPos(0, Math.Sign(dy));
                }

                return true;
            }

            if (targeting == TargetingType.Diagonal)
            {
                if (dx == 0 || dy == 0)
                {
                    return false;
                }

                direction = new GridPos(Math.Sign(dx), Math.Sign(dy));
                return true;
            }

            return false;
        }

        private void ResolveSingleAdjacent(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction,
            int damage)
        {
            GridPos target = origin.Offset(direction.X, direction.Y);
            EnemyInstance enemy = GetEnemyAt(engine.CurrentRun, target);

            if (enemy != null)
            {
                engine.ApplyDamageToEnemy(enemy, damage, card.HasPoison, card.Definition.Name);
            }
            else
            {
                engine.AddCombatLog($"Turn {engine.CurrentRun.TurnNumber}: {card.Definition.Name} swung at {target} (no target).");
            }
        }

        private void ResolveKnockbackAdjacent(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction,
            int damage)
        {
            GridPos target = origin.Offset(direction.X, direction.Y);
            EnemyInstance enemy = GetEnemyAt(engine.CurrentRun, target);

            if (enemy == null)
            {
                engine.AddCombatLog($"Turn {engine.CurrentRun.TurnNumber}: {card.Definition.Name} swung at {target} (no target).");
                return;
            }

            engine.ApplyDamageToEnemy(enemy, damage, card.HasPoison, card.Definition.Name);

            if (enemy.CurrentHp > 0)
            {
                GridPos pushDest = enemy.Position.Offset(direction.X, direction.Y);
                if (pushDest.IsValid(GridSize) &&
                    pushDest != engine.CurrentRun.Player.Position &&
                    GetEnemyAt(engine.CurrentRun, pushDest) == null)
                {
                    enemy.Position = pushDest;
                    engine.AddCombatLog($"{enemy.Name} was knocked back to {pushDest}.");
                    engine.TriggerHazardsForEnemy(enemy);
                }
            }
        }

        private void ResolveArcSweep(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction,
            int damage)
        {
            GridPos[] arcTiles = GetArcSweepTiles(origin, direction);
            int hits = 0;

            foreach (GridPos tile in arcTiles)
            {
                if (!tile.IsValid(GridSize))
                {
                    continue;
                }

                EnemyInstance enemy = GetEnemyAt(engine.CurrentRun, tile);
                if (enemy != null)
                {
                    engine.ApplyDamageToEnemy(enemy, damage, card.HasPoison, card.Definition.Name);
                    hits++;
                }
            }

            if (hits == 0)
            {
                engine.AddCombatLog($"Turn {engine.CurrentRun.TurnNumber}: {card.Definition.Name} swept 3 tiles (no enemies hit).");
            }
        }

        private void ResolveLineProjectile(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction,
            int damage,
            int maxRange,
            int maxHits)
        {
            int hitCount = 0;

            for (int step = 1; step <= maxRange; step++)
            {
                GridPos tile = origin.Offset(direction.X * step, direction.Y * step);
                if (!tile.IsValid(GridSize))
                {
                    break;
                }

                EnemyInstance enemy = GetEnemyAt(engine.CurrentRun, tile);
                if (enemy != null)
                {
                    engine.ApplyDamageToEnemy(enemy, damage, card.HasPoison, card.Definition.Name);
                    hitCount++;
                    if (hitCount >= maxHits)
                    {
                        break;
                    }
                }
            }

            if (hitCount == 0)
            {
                engine.AddCombatLog($"Turn {engine.CurrentRun.TurnNumber}: {card.Definition.Name} missed.");
            }
        }

        private void ResolvePlaceBearTrap(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            int damage)
        {
            ActiveRunState run = engine.CurrentRun;
            run.Hazards.RemoveAll(h => h.Type == HazardType.BearTrap && h.Position == origin);

            run.Hazards.Add(new GroundHazard
            {
                Type = HazardType.BearTrap,
                Position = origin,
                Damage = damage,
                RemainingTurns = -1,
                IsArmed = false,
                AppliesStun = true
            });

            engine.AddCombatLog($"Turn {run.TurnNumber}: Placed Bear Trap at {origin} (arms when you leave the tile).");
        }

        private void ResolveHarpoonPull(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction,
            int damage)
        {
            ActiveRunState run = engine.CurrentRun;
            EnemyInstance hitEnemy = null;
            int hitStep = 0;

            for (int step = 1; step <= 3; step++)
            {
                GridPos tile = origin.Offset(direction.X * step, direction.Y * step);
                if (!tile.IsValid(GridSize))
                {
                    break;
                }

                EnemyInstance candidate = GetEnemyAt(run, tile);
                if (candidate != null)
                {
                    hitEnemy = candidate;
                    hitStep = step;
                    break;
                }
            }

            if (hitEnemy == null)
            {
                engine.AddCombatLog($"Turn {run.TurnNumber}: {card.Definition.Name} missed (range 3).");
                return;
            }

            engine.ApplyDamageToEnemy(hitEnemy, damage, card.HasPoison, card.Definition.Name);

            if (hitEnemy.CurrentHp > 0 && hitStep > 1)
            {
                for (int pullStep = 1; pullStep < hitStep; pullStep++)
                {
                    GridPos pullTile = origin.Offset(direction.X * pullStep, direction.Y * pullStep);
                    if (pullTile.IsValid(GridSize) &&
                        pullTile != run.Player.Position &&
                        GetEnemyAt(run, pullTile) == null)
                    {
                        hitEnemy.Position = pullTile;
                        engine.AddCombatLog($"{hitEnemy.Name} was pulled to {pullTile}!");
                        engine.TriggerHazardsForEnemy(hitEnemy);
                        break;
                    }
                }
            }
        }

        private void ResolveDiagonalDash(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction)
        {
            ActiveRunState run = engine.CurrentRun;
            GridPos target = origin.Offset(direction.X, direction.Y);

            if (!target.IsValid(GridSize) || GetEnemyAt(run, target) != null)
            {
                engine.AddCombatLog($"Turn {run.TurnNumber}: {card.Definition.Name} to {target} was blocked.");
                return;
            }

            run.Player.Position = target;
            engine.ArmBearTrapAt(origin);
            engine.AddCombatLog($"Turn {run.TurnNumber}: Dashed diagonally to {target}.");
            engine.TriggerHazardsForPlayer();
        }

        private void ResolveSlideTwo(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction)
        {
            ActiveRunState run = engine.CurrentRun;
            GridPos destination = origin;

            for (int step = 1; step <= 2; step++)
            {
                GridPos nextTile = origin.Offset(direction.X * step, direction.Y * step);
                if (!nextTile.IsValid(GridSize) || GetEnemyAt(run, nextTile) != null)
                {
                    break;
                }

                destination = nextTile;
            }

            if (destination == origin)
            {
                engine.AddCombatLog($"Turn {run.TurnNumber}: {card.Definition.Name} was blocked.");
                return;
            }

            run.Player.Position = destination;
            engine.ArmBearTrapAt(origin);
            engine.AddCombatLog($"Turn {run.TurnNumber}: Slid to {destination}.");
            engine.TriggerHazardsForPlayer();
        }

        private void TraceLinePreview(
            ActiveRunState run,
            AimPreviewResult result,
            GridPos origin,
            GridPos direction,
            int maxRange,
            int maxHits)
        {
            int hits = 0;

            for (int step = 1; step <= maxRange; step++)
            {
                GridPos tile = origin.Offset(direction.X * step, direction.Y * step);
                if (!tile.IsValid(GridSize))
                {
                    break;
                }

                result.AffectedTiles.Add(tile);

                if (GetEnemyAt(run, tile) != null)
                {
                    result.HitEnemyTiles.Add(tile);
                    hits++;
                    if (hits >= maxHits)
                    {
                        break;
                    }
                }
            }
        }

        private void ComputeDiagonalDashPreview(
            ActiveRunState run,
            AimPreviewResult result,
            GridPos origin,
            GridPos direction)
        {
            GridPos target = origin.Offset(direction.X, direction.Y);
            if (target.IsValid(GridSize) && GetEnemyAt(run, target) == null)
            {
                result.AffectedTiles.Add(target);
                result.MoveDestination = target;
            }
        }

        private void ComputeSlidePreview(
            ActiveRunState run,
            AimPreviewResult result,
            GridPos origin,
            GridPos direction)
        {
            GridPos destination = origin;

            for (int step = 1; step <= 2; step++)
            {
                GridPos nextTile = origin.Offset(direction.X * step, direction.Y * step);
                if (!nextTile.IsValid(GridSize) || GetEnemyAt(run, nextTile) != null)
                {
                    break;
                }

                result.AffectedTiles.Add(nextTile);
                destination = nextTile;
            }

            if (destination != origin)
            {
                result.MoveDestination = destination;
            }
        }

        private void AddTileIfValid(ActiveRunState run, AimPreviewResult result, GridPos tile)
        {
            if (!tile.IsValid(GridSize))
            {
                return;
            }

            result.AffectedTiles.Add(tile);
            if (GetEnemyAt(run, tile) != null)
            {
                result.HitEnemyTiles.Add(tile);
            }
        }

        private static GridPos[] GetArcSweepTiles(GridPos origin, GridPos direction)
        {
            int dx = direction.X;
            int dy = direction.Y;

            return new GridPos[]
            {
                origin.Offset(dx - dy, dy + dx),
                origin.Offset(dx, dy),
                origin.Offset(dx + dy, dy - dx)
            };
        }

        private static bool IsOrthogonalDirection(GridPos dir)
        {
            return Math.Abs(dir.X) + Math.Abs(dir.Y) == 1;
        }

        private static bool IsDiagonalDirection(GridPos dir)
        {
            return Math.Abs(dir.X) == 1 && Math.Abs(dir.Y) == 1;
        }

        private static EnemyInstance GetEnemyAt(ActiveRunState run, GridPos pos)
        {
            return run.Enemies.FirstOrDefault(e => e.CurrentHp > 0 && e.Position == pos);
        }
    }
}