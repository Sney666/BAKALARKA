using System.Collections.Generic;
using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class HarpoonPullEffect : ICardEffect
    {
        public WeaponEffectType EffectType
        {
            get
            {
                return WeaponEffectType.BoatHook;
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
                EnemyInstance target = hitEnemies[0];
                GridPos pullDest = run.Player.Position.Offset(direction);
                preview.ImpactTiles.Add(target.Position);
                preview.Summary = target.Position != pullDest
                    ? $"Hook {target.Type} and pull to {pullDest}."
                    : $"Hook adjacent {target.Type} at {target.Position}.";
            }
            else
            {
                preview.Summary = $"Cast {card.Definition.Name} up to {tiles.Count} tile(s).";
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
                context.Log($"Cast {card.Definition.Name}, hooking nothing.");
                return;
            }

            EnemyInstance target = hitEnemies[0];
            context.DamageEnemy(target, damage, card.HasPoison, card.Definition.Name);

            if (!target.IsAlive)
            {
                return;
            }

            GridPos pullDestination = context.Run.Player.Position.Offset(direction);
            if (target.Position != pullDestination && pullDestination.IsValid() && !context.Run.IsTileOccupied(pullDestination))
            {
                target.Position = pullDestination;
                context.Log($"{card.Definition.Name} pulled {target.Type} to {pullDestination}.");
                context.TriggerHazardsForEnemy(target);
            }
        }
    }
}