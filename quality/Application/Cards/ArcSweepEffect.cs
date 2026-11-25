using System.Collections.Generic;
using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class ArcSweepEffect : ICardEffect
    {
        public WeaponEffectType EffectType
        {
            get
            {
                return WeaponEffectType.TwoHandedHammer;
            }
        }
        public AimPreviewResult ComputePreview(ActiveRunState run, CardInstance card, GridPos direction)
        {
            AimPreviewResult preview = new AimPreviewResult { Direction = direction };
            if (!direction.IsOrthogonalDirection())
            {
                return preview;
            }

            GridPos center = run.Player.Position.Offset(direction);
            if (!center.IsValid())
            {
                return preview;
            }

            List<GridPos> arcTiles = GridRayHelper.GetArcSweepTiles(run.Player.Position, direction);
            preview.IsValid = arcTiles.Count > 0;
            preview.AffectedTiles.AddRange(arcTiles);

            int hitCount = 0;
            foreach (GridPos tile in arcTiles)
            {
                if (run.GetEnemyAt(tile) != null)
                {
                    preview.ImpactTiles.Add(tile);
                    hitCount += 1;
                }
            }

            preview.Summary = hitCount > 0
                ? $"Arc sweep hits {hitCount} target(s)."
                : $"Sweep {card.Definition.Name} across {arcTiles.Count} tile(s).";
            return preview;
        }

        public void Execute(ICombatContext context, CardInstance card, GridPos direction, int damage)
        {
            List<GridPos> arcTiles = GridRayHelper.GetArcSweepTiles(context.Run.Player.Position, direction);
            List<EnemyInstance> targets = new List<EnemyInstance>();

            foreach (GridPos tile in arcTiles)
            {
                EnemyInstance? enemy = context.Run.GetEnemyAt(tile);
                if (enemy != null)
                {
                    targets.Add(enemy);
                }
            }

            if (targets.Count == 0)
            {
                context.Log($"Swept {card.Definition.Name} through empty tiles.");
                return;
            }

            foreach (EnemyInstance enemy in targets)
            {
                context.DamageEnemy(enemy, damage, card.HasPoison, card.Definition.Name);
            }
        }
    }
}