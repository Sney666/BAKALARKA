using System.Linq;
using RogueCards.Domain;
namespace RogueCards.Application.Enemies
{
    public sealed class ShamanBehavior : IEnemyBehavior
    {
        private const int PoisonPoolDuration = 3;
        public EnemyType EnemyType
        {
            get
            {
                return EnemyType.Shaman;
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

                LockPoisonPotion(context, enemy, playerPos);
                return;
            }

            if (distance == 3)
            {
                LockPoisonPotion(context, enemy, playerPos);
                return;
            }

            EnemyMovementHelper.TryStepToward(context, enemy, playerPos);
        }

        public void ExecuteChargedAttack(ICombatContext context, EnemyInstance enemy)
        {
            if (enemy.LockedTargetTiles.Count == 0)
            {
                EnemyMovementHelper.ResetAfterAttack(enemy);
                return;
            }

            int damage = context.Database.Enemies[EnemyType.Shaman].Damage;
            GridPos impactCenter = enemy.LockedTargetTiles[0];

            EnemyMovementHelper.StrikeTiles(context, enemy, new[] { impactCenter }, damage, "Poison Potion");

            foreach (GridPos poolPos in enemy.LockedTargetTiles)
            {
                PlaceOrRefreshPoisonPool(context.Run, poolPos);
                ApplyImmediatePoolPoison(context, poolPos);
            }

            context.Log($"Shaman's Poison Potion splashed toxic pools around {impactCenter} for {PoisonPoolDuration} turns!");
            EnemyMovementHelper.ResetAfterAttack(enemy);
        }

        private static void LockPoisonPotion(ICombatContext context, EnemyInstance enemy, GridPos targetCenter)
        {
            enemy.State = EnemyState.Charging;
            enemy.ChargeTurnsRemaining = 1;
            enemy.LockedDirection = EnemyMovementHelper.GetOrthogonalDirectionToward(enemy.Position, targetCenter);
            enemy.LockedAttackType = "Potion";
            enemy.LockedTargetTiles.Clear();
            enemy.LockedTargetTiles.Add(targetCenter);

            foreach (GridPos diag in GridPos.DiagonalDirections)
            {
                GridPos neighbor = targetCenter.Offset(diag);
                if (neighbor.IsValid())
                {
                    enemy.LockedTargetTiles.Add(neighbor);
                }
            }

            context.Log($"Shaman is preparing a Poison Potion targeting {targetCenter}!");
        }

        private static void PlaceOrRefreshPoisonPool(ActiveRunState run, GridPos pos)
        {
            GroundHazard? existing = run.Hazards.FirstOrDefault(
                h => h.Type == HazardType.PoisonPool && h.Position == pos);

            if (existing != null)
            {
                existing.RemainingTurns = PoisonPoolDuration;
                return;
            }

            run.Hazards.Add(new GroundHazard
            {
                Type = HazardType.PoisonPool,
                Position = pos,
                RemainingTurns = PoisonPoolDuration,
                Damage = 1,
                IsArmed = true,
                AppliesStun = false
            });
        }

        private static void ApplyImmediatePoolPoison(ICombatContext context, GridPos pos)
        {
            if (context.Run.Player.Position == pos && !context.Run.Player.IsImmuneThisTurn)
            {
                context.Run.Player.PoisonTurnsRemaining = PoisonPoolDuration;
            }

            EnemyInstance? victim = context.Run.GetEnemyAt(pos);
            if (victim != null && victim.IsAlive)
            {
                victim.PoisonTurnsRemaining = PoisonPoolDuration;
            }
        }
    }
}