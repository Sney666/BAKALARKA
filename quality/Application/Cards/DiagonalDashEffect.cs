using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class DiagonalDashEffect : ICardEffect
    {
        public WeaponEffectType EffectType
        {
            get
            {
                return WeaponEffectType.DiagonalDash;
            }
        }
        public AimPreviewResult ComputePreview(ActiveRunState run, CardInstance card, GridPos direction)
        {
            AimPreviewResult preview = new AimPreviewResult { Direction = direction };
            if (!direction.IsDiagonalDirection())
            {
                return preview;
            }

            GridPos destination = run.Player.Position.Offset(direction);
            if (!destination.IsValid() || run.IsTileOccupied(destination))
            {
                return preview;
            }

            preview.IsValid = true;
            preview.MoveDestination = destination;
            preview.AffectedTiles.Add(destination);
            preview.Summary = $"Dash diagonally to {destination}.";
            return preview;
        }

        public void Execute(ICombatContext context, CardInstance card, GridPos direction, int damage)
        {
            GridPos previousPos = context.Run.Player.Position;
            GridPos destination = previousPos.Offset(direction);

            if (!destination.IsValid() || context.Run.IsTileOccupied(destination))
            {
                return;
            }

            context.Run.Player.Position = destination;
            context.ArmBearTrapAt(previousPos);
            context.Log($"Used {card.Definition.Name} to dash to {destination}.");
            context.TriggerHazardsForPlayer();
        }
    }
}