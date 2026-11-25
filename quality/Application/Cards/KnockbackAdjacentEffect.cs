using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class KnockbackAdjacentEffect : ICardEffect
    {
        public WeaponEffectType EffectType
        {
            get
            {
                return WeaponEffectType.WoodenClub;
            }
        }
        public AimPreviewResult ComputePreview(ActiveRunState run, CardInstance card, GridPos direction)
        {
            AimPreviewResult preview = new AimPreviewResult { Direction = direction };
            if (!direction.IsOrthogonalDirection())
            {
                return preview;
            }

            GridPos targetTile = run.Player.Position.Offset(direction);
            if (!targetTile.IsValid())
            {
                return preview;
            }

            preview.IsValid = true;
            preview.AffectedTiles.Add(targetTile);

            EnemyInstance? enemy = run.GetEnemyAt(targetTile);
            if (enemy != null)
            {
                preview.ImpactTiles.Add(targetTile);
                GridPos pushTile = targetTile.Offset(direction);
                preview.Summary = pushTile.IsValid() && !run.IsTileOccupied(pushTile)
                    ? $"Hit {enemy.Type} and knock back to {pushTile}."
                    : $"Hit {enemy.Type} at {targetTile} (knockback blocked).";
            }
            else
            {
                preview.Summary = $"Swing {card.Definition.Name} toward {targetTile}.";
            }

            return preview;
        }

        public void Execute(ICombatContext context, CardInstance card, GridPos direction, int damage)
        {
            GridPos targetTile = context.Run.Player.Position.Offset(direction);
            if (!targetTile.IsValid())
            {
                return;
            }

            EnemyInstance? enemy = context.Run.GetEnemyAt(targetTile);
            if (enemy == null)
            {
                context.Log($"Swung {card.Definition.Name} at {targetTile}, hitting empty air.");
                return;
            }

            context.DamageEnemy(enemy, damage, card.HasPoison, card.Definition.Name);

            if (!enemy.IsAlive)
            {
                return;
            }

            GridPos pushTile = enemy.Position.Offset(direction);
            if (pushTile.IsValid() && !context.Run.IsTileOccupied(pushTile))
            {
                enemy.Position = pushTile;
                context.Log($"{card.Definition.Name} knocked {enemy.Type} back to {pushTile}.");
                context.TriggerHazardsForEnemy(enemy);
            }
        }
    }
}