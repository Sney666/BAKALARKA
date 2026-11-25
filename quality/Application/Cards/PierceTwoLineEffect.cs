using System.Collections.Generic;
using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class PierceTwoLineEffect : ICardEffect
    {
        private const int MaxPiercedTargets = 2;
        public WeaponEffectType EffectType
        {
            get
            {
                return WeaponEffectType.HeavySpear;
            }
        }

        public AimPreviewResult ComputePreview(ActiveRunState run, CardInstance card, GridPos direction)
        {
            AimPreviewResult preview = new AimPreviewResult { Direction = direction };
            if (!direction.IsOrthogonalDirection())
            {
                return preview;
            }

            List<GridPos> tiles = GridRayHelper.TraceRayTiles(
                run,
                run.Player.Position,
                direction,
                card.Definition.Range,
                MaxPiercedTargets,
                out List<EnemyInstance> hitEnemies);

            if (tiles.Count == 0)
            {
                return preview;
            }

            preview.IsValid = true;
            preview.AffectedTiles.AddRange(tiles);

            foreach (EnemyInstance enemy in hitEnemies)
            {
                preview.ImpactTiles.Add(enemy.Position);
            }

            preview.Summary = hitEnemies.Count > 0
                ? $"Pierce {hitEnemies.Count} target(s) in a line."
                : $"Hurl {card.Definition.Name} across {tiles.Count} tile(s).";
            return preview;
        }

        public void Execute(ICombatContext context, CardInstance card, GridPos direction, int damage)
        {
            GridRayHelper.TraceRayTiles(
                context.Run,
                context.Run.Player.Position,
                direction,
                card.Definition.Range,
                MaxPiercedTargets,
                out List<EnemyInstance> hitEnemies);

            if (hitEnemies.Count == 0)
            {
                context.Log($"Hurled {card.Definition.Name}, hitting no targets.");
                return;
            }

            foreach (EnemyInstance enemy in hitEnemies)
            {
                context.DamageEnemy(enemy, damage, card.HasPoison, card.Definition.Name);
            }
        }
    }
}