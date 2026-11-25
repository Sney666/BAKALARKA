using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class SingleAdjacentEffect : ICardEffect
    {
        public WeaponEffectType EffectType
        {
            get
            {
                return WeaponEffectType.VikingAxe;
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
                preview.Summary = $"Strike {enemy.Type} at {targetTile}.";
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
        }
    }
}