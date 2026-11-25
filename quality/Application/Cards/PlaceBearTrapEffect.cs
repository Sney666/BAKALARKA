using System.Linq;
using RogueCards.Domain;
namespace RogueCards.Application.Cards
{
    public sealed class PlaceBearTrapEffect : ICardEffect
    {
        public WeaponEffectType EffectType
        {
            get
            {
                return WeaponEffectType.BearTrap;
            }
        }
        public AimPreviewResult ComputePreview(ActiveRunState run, CardInstance card, GridPos direction)
        {
            AimPreviewResult preview = new AimPreviewResult
            {
                IsValid = true,
                Direction = new GridPos(0, 0),
                Summary = $"Place an unarmed Bear Trap at {run.Player.Position}."
            };
            preview.AffectedTiles.Add(run.Player.Position);
            return preview;
        }

        public void Execute(ICombatContext context, CardInstance card, GridPos direction, int damage)
        {
            GridPos pos = context.Run.Player.Position;
            GroundHazard? existing = context.Run.Hazards.FirstOrDefault(
                h => h.Type == HazardType.BearTrap && h.Position == pos);

            if (existing != null)
            {
                existing.Damage = damage;
                existing.IsArmed = false;
                existing.AppliesStun = true;
                context.Log($"Replaced Bear Trap at {pos} (arms when you leave).");
                return;
            }

            GroundHazard trap = new GroundHazard
            {
                Type = HazardType.BearTrap,
                Position = pos,
                RemainingTurns = -1,
                Damage = damage,
                IsArmed = false,
                AppliesStun = true
            };

            context.Run.Hazards.Add(trap);
            context.Log($"Placed Bear Trap at {pos} (arms when you leave).");
        }
    }
}