#nullable disable

namespace RogueCards
{
    public class AimPreviewResult
    {
        public List<GridPos> AffectedTiles { get; } = [];
        public List<GridPos> HitEnemyTiles { get; } = [];
        public GridPos? MoveDestination { get; set; }
        public bool IsMovement { get; set; }
        public bool IsValidAim { get; set; }
    }

    public static class CardResolver
    {
        private const int GridSize = GameEngine.GridSize;

        public static AimPreviewResult ComputeAimPreview(
            ActiveRunState run,
            CardInstance card,
            GridPos direction)
        {
            AimPreviewResult result = new() { IsValidAim = false };
            if (run?.Player == null || card?.Definition == null)
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

            if (!IsDirectionValidForTargeting(def.Targeting, direction))
            {
                return result;
            }

            result.IsValidAim = true;
            result.IsMovement = def.Category == CardCategory.Movement;
            PopulatePreviewByEffect(run, result, def, origin, direction);
            return result;
        }

        private static void PopulatePreviewByEffect(
            ActiveRunState run,
            AimPreviewResult result,
            WeaponDefinition def,
            GridPos origin,
            GridPos direction)
        {
            int lineRange = def.Range > 0 ? def.Range : GridSize;
            switch (def.Effect)
            {
                case WeaponEffectType.SingleAdjacent:
                case WeaponEffectType.KnockbackAdjacent:
                    AddTileIfValid(run, result, origin.Offset(direction.X, direction.Y));
                    break;
                case WeaponEffectType.ArcSweep:
                    foreach (GridPos tile in GetArcSweepTiles(origin, direction))
                    {
                        AddTileIfValid(run, result, tile);
                    }

                    break;
                case WeaponEffectType.LineProjectile:
                case WeaponEffectType.DiagonalProjectile:
                    TraceLinePreview(run, result, origin, direction, lineRange, 1);
                    break;
                case WeaponEffectType.PierceTwoLine:
                    TraceLinePreview(run, result, origin, direction, lineRange, 2);
                    break;
                case WeaponEffectType.HarpoonPull:
                    TraceLinePreview(run, result, origin, direction, def.Range > 0 ? def.Range : 3, 1);
                    break;
                case WeaponEffectType.DiagonalDash:
                    ComputeDiagonalDashPreview(run, result, origin, direction);
                    break;
                case WeaponEffectType.SlideTwo:
                    ComputeSlidePreview(run, result, origin, direction, def.Range > 0 ? def.Range : 2);
                    break;
                case WeaponEffectType.PlaceBearTrap:
                    result.AffectedTiles.Add(origin);
                    break;
            }
        }

        public static bool ResolveCard(GameEngine engine, CardInstance card, GridPos direction)
        {
            if (engine?.CurrentRun == null || card?.Definition == null)
            {
                return false;
            }

            WeaponDefinition def = card.Definition;
            if (!IsDirectionValidForTargeting(def.Targeting, direction))
            {
                return false;
            }

            int damage = engine.GetEffectiveCardDamage(card);
            GridPos origin = engine.CurrentRun.Player.Position;
            int lineRange = def.Range > 0 ? def.Range : GridSize;

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
                    ResolveLineProjectile(engine, card, origin, direction, damage, lineRange, 1);
                    break;
                case WeaponEffectType.PierceTwoLine:
                    ResolveLineProjectile(engine, card, origin, direction, damage, lineRange, 2);
                    break;
                case WeaponEffectType.PlaceBearTrap:
                    ResolvePlaceBearTrap(engine, origin, damage);
                    break;
                case WeaponEffectType.HarpoonPull:
                    ResolveHarpoonPull(engine, card, origin, direction, damage, def.Range > 0 ? def.Range : 3);
                    break;
                case WeaponEffectType.DiagonalDash:
                    ResolveDiagonalDash(engine, card, origin, direction);
                    break;
                case WeaponEffectType.SlideTwo:
                    ResolveSlideTwo(engine, card, origin, direction, def.Range > 0 ? def.Range : 2);
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
                direction = Math.Abs(dx) >= Math.Abs(dy)
                    ? new GridPos(Math.Sign(dx), 0)
                    : new GridPos(0, Math.Sign(dy));
                return true;
            }

            if (targeting == TargetingType.Diagonal && dx != 0 && dy != 0)
            {
                direction = new GridPos(Math.Sign(dx), Math.Sign(dy));
                return true;
            }

            return false;
        }

        private static bool IsDirectionValidForTargeting(TargetingType targeting, GridPos direction) => targeting switch
        {
            TargetingType.Orthogonal => IsOrthogonalDirection(direction),
            TargetingType.Diagonal => IsDiagonalDirection(direction),
            _ => true
        };

        private static void ResolveSingleAdjacent(
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

        private static void ResolveKnockbackAdjacent(
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

            if (enemy.CurrentHp <= 0)
            {
                return;
            }

            GridPos pushDest = enemy.Position.Offset(direction.X, direction.Y);
            if (pushDest.IsValid() &&
                pushDest != engine.CurrentRun.Player.Position &&
                GetEnemyAt(engine.CurrentRun, pushDest) == null)
            {
                enemy.Position = pushDest;
                engine.AddCombatLog($"{enemy.Name} was knocked back to {pushDest}.");
                engine.TriggerHazardsForEnemy(enemy);
            }
        }

        private static void ResolveArcSweep(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction,
            int damage)
        {
            int hits = 0;
            foreach (GridPos tile in GetArcSweepTiles(origin, direction))
            {
                if (!tile.IsValid())
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

        private static void ResolveLineProjectile(
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
                if (!tile.IsValid())
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

        private static void ResolvePlaceBearTrap(GameEngine engine, GridPos origin, int damage)
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

        private static void ResolveHarpoonPull(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction,
            int damage,
            int maxRange)
        {
            ActiveRunState run = engine.CurrentRun;
            (EnemyInstance hitEnemy, int hitStep) = FindFirstEnemyInRay(run, origin, direction, maxRange);

            if (hitEnemy == null)
            {
                engine.AddCombatLog($"Turn {run.TurnNumber}: {card.Definition.Name} missed (range {maxRange}).");
                return;
            }

            engine.ApplyDamageToEnemy(hitEnemy, damage, card.HasPoison, card.Definition.Name);
            if (hitEnemy.CurrentHp > 0 && hitStep > 1)
            {
                TryPullEnemyTowardOrigin(engine, hitEnemy, origin, direction, hitStep);
            }
        }

        private static (EnemyInstance Enemy, int Step) FindFirstEnemyInRay(
            ActiveRunState run,
            GridPos origin,
            GridPos direction,
            int maxRange)
        {
            for (int step = 1; step <= maxRange; step++)
            {
                GridPos tile = origin.Offset(direction.X * step, direction.Y * step);
                if (!tile.IsValid())
                {
                    break;
                }

                EnemyInstance candidate = GetEnemyAt(run, tile);
                if (candidate != null)
                {
                    return (candidate, step);
                }
            }

            return (null, 0);
        }

        private static void TryPullEnemyTowardOrigin(
            GameEngine engine,
            EnemyInstance enemy,
            GridPos origin,
            GridPos direction,
            int hitStep)
        {
            ActiveRunState run = engine.CurrentRun;
            for (int pullStep = 1; pullStep < hitStep; pullStep++)
            {
                GridPos pullTile = origin.Offset(direction.X * pullStep, direction.Y * pullStep);
                if (pullTile.IsValid() && pullTile != run.Player.Position && GetEnemyAt(run, pullTile) == null)
                {
                    enemy.Position = pullTile;
                    engine.AddCombatLog($"{enemy.Name} was pulled to {pullTile}!");
                    engine.TriggerHazardsForEnemy(enemy);
                    return;
                }
            }
        }

        private static void ResolveDiagonalDash(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction)
        {
            ActiveRunState run = engine.CurrentRun;
            GridPos target = origin.Offset(direction.X, direction.Y);

            if (!target.IsValid() || GetEnemyAt(run, target) != null)
            {
                engine.AddCombatLog($"Turn {run.TurnNumber}: {card.Definition.Name} to {target} was blocked.");
                return;
            }

            run.Player.Position = target;
            engine.ArmBearTrapAt(origin);
            engine.AddCombatLog($"Turn {run.TurnNumber}: Dashed diagonally to {target}.");
            engine.TriggerHazardsForPlayer();
        }

        private static void ResolveSlideTwo(
            GameEngine engine,
            CardInstance card,
            GridPos origin,
            GridPos direction,
            int maxSteps)
        {
            ActiveRunState run = engine.CurrentRun;
            GridPos destination = origin;

            for (int step = 1; step <= maxSteps; step++)
            {
                GridPos nextTile = origin.Offset(direction.X * step, direction.Y * step);
                if (!nextTile.IsValid() || GetEnemyAt(run, nextTile) != null)
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

        private static void TraceLinePreview(
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
                if (!tile.IsValid())
                {
                    break;
                }

                result.AffectedTiles.Add(tile);
                if (GetEnemyAt(run, tile) == null)
                {
                    continue;
                }

                result.HitEnemyTiles.Add(tile);
                hits++;
                if (hits >= maxHits)
                {
                    break;
                }
            }
        }

        private static void ComputeDiagonalDashPreview(
            ActiveRunState run,
            AimPreviewResult result,
            GridPos origin,
            GridPos direction)
        {
            GridPos target = origin.Offset(direction.X, direction.Y);
            if (target.IsValid() && GetEnemyAt(run, target) == null)
            {
                result.AffectedTiles.Add(target);
                result.MoveDestination = target;
            }
        }

        private static void ComputeSlidePreview(
            ActiveRunState run,
            AimPreviewResult result,
            GridPos origin,
            GridPos direction,
            int maxSteps)
        {
            GridPos destination = origin;

            for (int step = 1; step <= maxSteps; step++)
            {
                GridPos nextTile = origin.Offset(direction.X * step, direction.Y * step);
                if (!nextTile.IsValid() || GetEnemyAt(run, nextTile) != null)
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

        private static void AddTileIfValid(ActiveRunState run, AimPreviewResult result, GridPos tile)
        {
            if (!tile.IsValid())
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
            return
            [
                origin.Offset(dx - dy, dy + dx),
                origin.Offset(dx, dy),
                origin.Offset(dx + dy, dy - dx)
            ];
        }

        private static bool IsOrthogonalDirection(GridPos dir) => Math.Abs(dir.X) + Math.Abs(dir.Y) == 1;

        private static bool IsDiagonalDirection(GridPos dir) => Math.Abs(dir.X) == 1 && Math.Abs(dir.Y) == 1;

        private static EnemyInstance GetEnemyAt(ActiveRunState run, GridPos pos) =>
            run.Enemies.FirstOrDefault(e => e.CurrentHp > 0 && e.Position == pos);
    }
}