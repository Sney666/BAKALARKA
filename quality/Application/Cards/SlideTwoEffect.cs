using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class SlideTwoEffect : ICardEffect
    {
        public WeaponEffectType EffectType
        {
            get
            {
                return WeaponEffectType.Slide;
            }
        }
        public AimPreviewResult ComputePreview(ActiveRunState run, CardInstance card, GridPos direction)
        {
            AimPreviewResult preview = new AimPreviewResult { Direction = direction };
            if (!direction.IsOrthogonalDirection())
            {
                return preview;
            }

            GridPos? destination = null;
            for (int step = 1; step <= card.Definition.Range; step++)
            {
                GridPos candidate = run.Player.Position.Offset(direction.X * step, direction.Y * step);
                if (!candidate.IsValid() || run.IsTileOccupied(candidate))
                {
                    break;
                }

                preview.AffectedTiles.Add(candidate);
                destination = candidate;
            }

            if (!destination.HasValue)
            {
                return preview;
            }

            preview.IsValid = true;
            preview.MoveDestination = destination.Value;
            preview.Summary = $"Slide to {destination.Value}.";
            return preview;
        }

        public void Execute(ICombatContext context, CardInstance card, GridPos direction, int damage)
        {
            AimPreviewResult preview = ComputePreview(context.Run, card, direction);
            if (!preview.IsValid || !preview.MoveDestination.HasValue)
            {
                return;
            }

            GridPos previousPos = context.Run.Player.Position;
            GridPos destination = preview.MoveDestination.Value;

            context.Run.Player.Position = destination;
            context.ArmBearTrapAt(previousPos);
            context.Log($"Used {card.Definition.Name} to slide to {destination}.");
            context.TriggerHazardsForPlayer();
        }
    }
}