using System.Collections.Generic;
using System.Drawing;
using RogueCards.Application;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public interface IScreenRenderer
    {
        void Render(Graphics g, UiRenderState state);
    }
    public sealed class UiHitTarget
    {
        public string Action { get; }

        public string Payload { get; }

        public int Index { get; }

        public Rectangle Bounds { get; }

        public bool Enabled { get; }

        public UiHitTarget(string action, Rectangle bounds, string payload = "", int index = -1, bool enabled = true)
        {
            Action = action;
            Bounds = bounds;
            Payload = payload;
            Index = index;
            Enabled = enabled;
        }
    }

    public sealed class UiRenderState
    {
        public GameEngine Engine { get; }

        public Point MousePosition { get; set; }

        public float PulsePhase { get; set; }

        public string BannerMessage { get; set; } = string.Empty;

        public GridPos? HoveredGridTile { get; set; }

        public AimPreviewResult? CurrentAimPreview { get; set; }

        public int DraggingEquippedIndex { get; set; } = -1;

        public Point DragCurrentPoint { get; set; }

        public UpgradeModifierType? SelectedUpgradeModifier { get; set; }

        public List<UiHitTarget> HitTargets { get; } = new List<UiHitTarget>();

        public UiRenderState(GameEngine engine)
        {
            Engine = engine;
        }

        public void BeginFrame()
        {
            HitTargets.Clear();
        }

        public void RegisterHit(string action, Rectangle bounds, string payload = "", int index = -1, bool enabled = true)
        {
            HitTargets.Add(new UiHitTarget(action, bounds, payload, index, enabled));
        }

        public bool IsHovered(Rectangle bounds)
        {
            return bounds.Contains(MousePosition);
        }

        public UiHitTarget? FindHitAt(Point point)
        {
            for (int i = HitTargets.Count - 1; i >= 0; i--)
            {
                UiHitTarget target = HitTargets[i];
                if (target.Enabled && target.Bounds.Contains(point))
                {
                    return target;
                }
            }

            return null;
        }
    }
}