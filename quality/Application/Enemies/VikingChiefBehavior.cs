using System;
using System.Collections.Generic;
using RogueCards.Domain;
namespace RogueCards.Application.Enemies
{
    public sealed class VikingChiefBehavior : IEnemyBehavior
    {
        private const string AttackThrowingAxe = "Throwing Axe";
        private const string AttackHookClub = "Hook+Club";
        private const string AttackEvolvedAxe = "Evolved Axe";
        private static readonly EnemyType[] SummonPool =
    {
        EnemyType.Pawn,
        EnemyType.Archer,
        EnemyType.Spearman,
        EnemyType.Shaman
    };

        private readonly Random _random;

        public EnemyType EnemyType
        {
            get
            {
                return EnemyType.VikingChief;
            }
        }

        public VikingChiefBehavior(Random? random = null)
        {
            _random = random ?? new Random();
        }

        public void DecideAction(ICombatContext context, EnemyInstance enemy)
        {
            int phase = enemy.BossCycleIndex % 3;
            if (phase == 0)
            {
                DecideThrowingAxePhase(context, enemy);
                return;
            }

            if (phase == 1)
            {
                DecideHookClubPhase(context, enemy);
                return;
            }

            DecideEvolvedAxePhase(context, enemy);
        }

        public void ExecuteChargedAttack(ICombatContext context, EnemyInstance enemy)
        {
            string attackType = enemy.LockedAttackType;

            if (attackType == AttackThrowingAxe)
            {
                EnemyMovementHelper.StrikeFirstEntityOnRay(context, enemy, enemy.LockedDirection, 5, 2, AttackThrowingAxe);
                enemy.BossCycleIndex = 1;
            }
            else if (attackType == AttackHookClub)
            {
                ExecuteHookAndClub(context, enemy);
                enemy.BossCycleIndex = 2;
            }
            else
            {
                EnemyMovementHelper.StrikeTiles(context, enemy, enemy.LockedTargetTiles, 3, AttackEvolvedAxe);
                enemy.BossCycleIndex = 0;
            }

            EnemyMovementHelper.ResetAfterAttack(enemy);
        }

        private static void DecideThrowingAxePhase(ICombatContext context, EnemyInstance enemy)
        {
            GridPos playerPos = context.Run.Player.Position;
            if (!EnemyMovementHelper.IsAlignedOrthogonally(enemy.Position, playerPos))
            {
                if (EnemyMovementHelper.TryAlignRowOrColumn(context, enemy, playerPos))
                {
                    return;
                }
            }

            GridPos dir = EnemyMovementHelper.GetOrthogonalDirectionToward(enemy.Position, playerPos);
            List<GridPos> tiles = EnemyMovementHelper.CollectRayTiles(enemy.Position, dir, 5);

            enemy.State = EnemyState.Charging;
            enemy.ChargeTurnsRemaining = 1;
            enemy.LockedDirection = dir;
            enemy.LockedAttackType = AttackThrowingAxe;
            enemy.LockedTargetTiles.Clear();
            enemy.LockedTargetTiles.AddRange(tiles);
            context.Log($"Viking Chief (Phase 1) is charging a Throwing Axe line toward {dir}!");
        }

        private void DecideHookClubPhase(ICombatContext context, EnemyInstance enemy)
        {
            GridPos playerPos = context.Run.Player.Position;
            bool inHookLine = EnemyMovementHelper.IsAlignedOrthogonally(enemy.Position, playerPos)
                && enemy.Position.ManhattanDistance(playerPos) <= 3;

            if (!inHookLine && EnemyMovementHelper.TryStepToward(context, enemy, playerPos))
            {
                return;
            }

            GridPos dir = EnemyMovementHelper.GetOrthogonalDirectionToward(enemy.Position, playerPos);
            List<GridPos> tiles = EnemyMovementHelper.CollectRayTiles(enemy.Position, dir, 3);

            enemy.State = EnemyState.Charging;
            enemy.ChargeTurnsRemaining = 2;
            enemy.LockedDirection = dir;
            enemy.LockedAttackType = AttackHookClub;
            enemy.LockedTargetTiles.Clear();
            enemy.LockedTargetTiles.AddRange(tiles);
            context.Log($"Viking Chief (Phase 2) begins a 2-turn Boat Hook + Wooden Club charge toward {dir}!");

            EnemyType summonType = SummonPool[_random.Next(SummonPool.Length)];
            EnemyInstance? summoned = context.SpawnEnemy(summonType);
            if (summoned != null)
            {
                context.Log($"Viking Chief summoned a {summonType} reinforcement!");
            }
        }

        private static void DecideEvolvedAxePhase(ICombatContext context, EnemyInstance enemy)
        {
            GridPos playerPos = context.Run.Player.Position;
            GridPos dir = EnemyMovementHelper.GetOrthogonalDirectionToward(enemy.Position, playerPos);
            List<GridPos> cleaveTiles = BuildEvolvedAxeTiles(enemy.Position, dir);

            if (!cleaveTiles.Contains(playerPos) && EnemyMovementHelper.TryStepToward(context, enemy, playerPos))
            {
                return;
            }

            enemy.State = EnemyState.Charging;
            enemy.ChargeTurnsRemaining = 1;
            enemy.LockedDirection = dir;
            enemy.LockedAttackType = AttackEvolvedAxe;
            enemy.LockedTargetTiles.Clear();
            enemy.LockedTargetTiles.AddRange(cleaveTiles);
            context.Log($"Viking Chief (Phase 3) is charging Evolved Viking Axe across {cleaveTiles.Count} tiles!");
        }

        private static void ExecuteHookAndClub(ICombatContext context, EnemyInstance boss)
        {
            GridPos dir = boss.LockedDirection;
            GridPos adjacentTile = boss.Position.Offset(dir);

            PullFirstTargetOnHook(context, boss, dir, adjacentTile);
            StrikeAdjacentWithClub(context, boss, dir, adjacentTile);
        }

        private static void PullFirstTargetOnHook(
            ICombatContext context,
            EnemyInstance boss,
            GridPos dir,
            GridPos pullDestination)
        {
            for (int step = 1; step <= 3; step++)
            {
                GridPos tile = boss.Position.Offset(dir.X * step, dir.Y * step);
                if (!tile.IsValid())
                {
                    break;
                }

                if (context.Run.Player.Position == tile)
                {
                    context.DamagePlayer(boss, 1, "Boss Boat Hook");
                    if (context.Run.Player.CurrentHp > 0 && tile != pullDestination && !context.Run.IsTileOccupied(pullDestination))
                    {
                        GridPos previousPos = context.Run.Player.Position;
                        context.Run.Player.Position = pullDestination;
                        context.ArmBearTrapAt(previousPos);
                        context.Log($"Viking Chief pulled the player to {pullDestination}!");
                        context.TriggerHazardsForPlayer();
                    }

                    return;
                }

                EnemyInstance? victim = context.Run.GetEnemyAt(tile);
                if (victim != null && victim.InstanceId != boss.InstanceId)
                {
                    context.DamageEnemyByFriendlyFire(boss, victim, 1, "Boss Boat Hook");
                    if (victim.IsAlive && tile != pullDestination && !context.Run.IsTileOccupied(pullDestination))
                    {
                        victim.Position = pullDestination;
                        context.Log($"Viking Chief pulled {victim.Type} to {pullDestination}!");
                        context.TriggerHazardsForEnemy(victim);
                    }

                    return;
                }
            }
        }

        private static void StrikeAdjacentWithClub(
            ICombatContext context,
            EnemyInstance boss,
            GridPos dir,
            GridPos adjacentTile)
        {
            if (!adjacentTile.IsValid())
            {
                return;
            }

            GridPos knockbackTile = adjacentTile.Offset(dir);

            if (context.Run.Player.Position == adjacentTile)
            {
                context.DamagePlayer(boss, 2, "Boss Wooden Club");
                if (context.Run.Player.CurrentHp > 0 && knockbackTile.IsValid() && !context.Run.IsTileOccupied(knockbackTile))
                {
                    GridPos previousPos = context.Run.Player.Position;
                    context.Run.Player.Position = knockbackTile;
                    context.ArmBearTrapAt(previousPos);
                    context.Log($"Viking Chief's Wooden Club knocked the player back to {knockbackTile}!");
                    context.TriggerHazardsForPlayer();
                }

                return;
            }

            EnemyInstance? victim = context.Run.GetEnemyAt(adjacentTile);
            if (victim != null && victim.InstanceId != boss.InstanceId)
            {
                context.DamageEnemyByFriendlyFire(boss, victim, 2, "Boss Wooden Club");
                if (victim.IsAlive && knockbackTile.IsValid() && !context.Run.IsTileOccupied(knockbackTile))
                {
                    victim.Position = knockbackTile;
                    context.Log($"Viking Chief's Wooden Club knocked {victim.Type} back to {knockbackTile}!");
                    context.TriggerHazardsForEnemy(victim);
                }
            }
        }

        private static List<GridPos> BuildEvolvedAxeTiles(GridPos origin, GridPos dir)
        {
            GridPos left = new GridPos(-dir.Y, dir.X);
            GridPos right = new GridPos(dir.Y, -dir.X);

            GridPos[] candidates =
            {
            origin.Offset(dir),
            origin.Offset(dir.X * 2, dir.Y * 2),
            origin.Offset(dir).Offset(left),
            origin.Offset(dir).Offset(right),
            origin.Offset(left),
            origin.Offset(right)
        };

            List<GridPos> validTiles = new List<GridPos>();
            foreach (GridPos tile in candidates)
            {
                if (tile.IsValid())
                {
                    validTiles.Add(tile);
                }
            }

            return validTiles;
        }
    }
}