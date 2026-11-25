using System.Collections.Generic;
using RogueCards.Domain;
namespace RogueCards.Application.Enemies
{
    public sealed class ArcherBehavior : IEnemyBehavior
    {
        public EnemyType EnemyType
        {
            get
            {
                return EnemyType.Archer;
            }
        }
        public void DecideAction(ICombatContext context, EnemyInstance enemy)
        {
            GridPos playerPos = context.Run.Player.Position;
            int distance = enemy.Position.ManhattanDistance(playerPos);

            if (distance <= 2)
            {
                if (EnemyMovementHelper.TryStepAway(context, enemy, playerPos))
                {
                    return;
                }

                LockBowShot(context, enemy, playerPos);
                return;
            }

            if (EnemyMovementHelper.IsAlignedOrthogonally(enemy.Position, playerPos))
            {
                LockBowShot(context, enemy, playerPos);
                return;
            }

            EnemyMovementHelper.TryAlignRowOrColumn(context, enemy, playerPos, minPreferredDistance: 3);
        }

        public void ExecuteChargedAttack(ICombatContext context, EnemyInstance enemy)
        {
            EnemyDefinition def = context.Database.Enemies[EnemyType.Archer];
            EnemyMovementHelper.StrikeFirstEntityOnRay(
                context,
                enemy,
                enemy.LockedDirection,
                def.Range,
                def.Damage,
                "Bow");
            EnemyMovementHelper.ResetAfterAttack(enemy);
        }

        private static void LockBowShot(ICombatContext context, EnemyInstance enemy, GridPos playerPos)
        {
            int range = context.Database.Enemies[EnemyType.Archer].Range;
            GridPos direction = EnemyMovementHelper.GetOrthogonalDirectionToward(enemy.Position, playerPos);
            List<GridPos> rayTiles = EnemyMovementHelper.CollectRayTiles(enemy.Position, direction, range);

            if (rayTiles.Count == 0)
            {
                return;
            }

            enemy.State = EnemyState.Charging;
            enemy.ChargeTurnsRemaining = 1;
            enemy.LockedDirection = direction;
            enemy.LockedAttackType = "Bow";
            enemy.LockedTargetTiles.Clear();
            enemy.LockedTargetTiles.AddRange(rayTiles);
            context.Log($"Archer is aiming a Bow shot along {direction}!");
        }
    }
}