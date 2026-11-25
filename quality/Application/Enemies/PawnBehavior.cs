using RogueCards.Domain;
namespace RogueCards.Application.Enemies
{
    public sealed class PawnBehavior : IEnemyBehavior
    {
        public EnemyType EnemyType
        {
            get
            {
                return EnemyType.Pawn;
            }
        }
        public void DecideAction(ICombatContext context, EnemyInstance enemy)
        {
            GridPos playerPos = context.Run.Player.Position;
            int distance = enemy.Position.ManhattanDistance(playerPos);

            if (distance == 1)
            {
                LockAxeStrike(context, enemy, playerPos);
                return;
            }

            EnemyMovementHelper.TryStepToward(context, enemy, playerPos);
        }

        public void ExecuteChargedAttack(ICombatContext context, EnemyInstance enemy)
        {
            int damage = context.Database.Enemies[EnemyType.Pawn].Damage;
            EnemyMovementHelper.StrikeTiles(context, enemy, enemy.LockedTargetTiles, damage, "Axe");
            EnemyMovementHelper.ResetAfterAttack(enemy);
        }

        private static void LockAxeStrike(ICombatContext context, EnemyInstance enemy, GridPos targetPos)
        {
            enemy.State = EnemyState.Charging;
            enemy.ChargeTurnsRemaining = 1;
            enemy.LockedDirection = EnemyMovementHelper.GetOrthogonalDirectionToward(enemy.Position, targetPos);
            enemy.LockedAttackType = "Axe";
            enemy.LockedTargetTiles.Clear();
            enemy.LockedTargetTiles.Add(targetPos);
            context.Log($"Pawn is charging an Axe strike at {targetPos}!");
        }
    }
}