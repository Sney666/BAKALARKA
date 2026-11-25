using System.Collections.Generic;
using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class LineProjectileEffect : ICardEffect
    {
        private readonly TargetingType _targeting;
        public WeaponEffectType EffectType { get; }

        public LineProjectileEffect(WeaponEffectType effectType, TargetingType targeting)
        {
            EffectType = effectType;
            _targeting = targeting;
        }

        public AimPreviewResult ComputePreview(ActiveRunState run, CardInstance card, GridPos direction)
        {
            AimPreviewResult preview = new AimPreviewResult { Direction = direction };
            if (!GridRayHelper.IsValidDirection(_targeting, direction))
            {
                return preview;
            }

            List<GridPos> tiles = GridRayHelper.TraceRayTiles(
                run,
                run.Player.Position,
                direction,
                card.Definition.Range,
                1,
                out List<EnemyInstance> hitEnemies);

            if (tiles.Count == 0)
            {
                return preview;
            }

            preview.IsValid = true;
            preview.AffectedTiles.AddRange(tiles);

            if (hitEnemies.Count > 0)
            {
                EnemyInstance firstTarget = hitEnemies[0];
                preview.ImpactTiles.Add(firstTarget.Position);
                preview.Summary = $"Hit {firstTarget.Type} at {firstTarget.Position}.";
            }
            else
            {
                preview.Summary = $"Fire {card.Definition.Name} along {tiles.Count} tile(s).";
            }

            return preview;
        }

        public void Execute(ICombatContext context, CardInstance card, GridPos direction, int damage)
        {
            GridRayHelper.TraceRayTiles(
                context.Run,
                context.Run.Player.Position,
                direction,
                card.Definition.Range,
                1,
                out List<EnemyInstance> hitEnemies);

            if (hitEnemies.Count == 0)
            {
                context.Log($"Fired {card.Definition.Name}, hitting no targets.");
                return;
            }

            context.DamageEnemy(hitEnemies[0], damage, card.HasPoison, card.Definition.Name);
        }
    }
}