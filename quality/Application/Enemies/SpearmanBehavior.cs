using System.Collections.Generic;
using RogueCards.Domain;
namespace RogueCards.Application.Enemies
{
    public sealed class SpearmanBehavior : IEnemyBehavior
    {
        public EnemyType EnemyType
        {
            get
            {
                return EnemyType.Spearman;
            }
        }
        public void DecideAction(ICombatContext context, EnemyInstance enemy)
        {
            GridPos playerPos = context.Run.Player.Position;
            int range = context.Database.Enemies[EnemyType.Spearman].Range;
            int distance = enemy.Position.ManhattanDistance(playerPos);

            if (EnemyMovementHelper.IsAlignedOrthogonally(enemy.Position, playerPos) && distance <= range)
            {
                LockSpearThrust(context, enemy, playerPos, range);
                return;
            }

            EnemyMovementHelper.TryStepToward(context, enemy, playerPos);
        }

        public void ExecuteChargedAttack(ICombatContext context, EnemyInstance enemy)
        {
            int damage = context.Database.Enemies[EnemyType.Spearman].Damage;
            EnemyMovementHelper.StrikeTiles(context, enemy, enemy.LockedTargetTiles, damage, "Spear");
            EnemyMovementHelper.ResetAfterAttack(enemy);
        }

        private static void LockSpearThrust(ICombatContext context, EnemyInstance enemy, GridPos playerPos, int range)
        {
            GridPos direction = EnemyMovementHelper.GetOrthogonalDirectionToward(enemy.Position, playerPos);
            List<GridPos> tiles = EnemyMovementHelper.CollectRayTiles(enemy.Position, direction, range);

            if (tiles.Count == 0)
            {
                return;
            }

            enemy.State = EnemyState.Charging;
            enemy.ChargeTurnsRemaining = 1;
            enemy.LockedDirection = direction;
            enemy.LockedAttackType = "Spear";
            enemy.LockedTargetTiles.Clear();
            enemy.LockedTargetTiles.AddRange(tiles);
            context.Log($"Spearman is charging a 2-tile Spear thrust toward {direction}!");
        }
    }
}