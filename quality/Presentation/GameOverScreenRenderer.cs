using System.Drawing;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public sealed class GameOverScreenRenderer : IScreenRenderer
    {
        public void Render(Graphics g, UiRenderState state)
        {
            UiPrimitives.DrawScreenBackground(g);
            ActiveRunState? run = state.Engine.CurrentRun;
            bool isVictory = run != null && run.IsVictory;

            Rectangle card = new Rectangle(240, 82, 800, 556);
            Color borderColor = isVictory ? UiTheme.BorderGold : UiTheme.MeleeHeader;
            UiPrimitives.DrawGradientPanel(
                g,
                card,
                14,
                Color.FromArgb(30, 38, 56),
                Color.FromArgb(20, 25, 38),
                borderColor,
                2.5f);

            DrawBannerTitle(g, card, isVictory, run?.GameOverReason ?? "Run ended.");
            DrawRunSummaryStats(g, state, card, run);
            DrawActionButtons(g, state, card);

            UiPrimitives.DrawAchievementToast(g, state.Engine.Achievements.ActiveToast);
        }

        private static void DrawBannerTitle(Graphics g, Rectangle card, bool isVictory, string reason)
        {
            using SolidBrush titleBrush = new SolidBrush(isVictory ? UiTheme.AccentGold : Color.FromArgb(236, 94, 102));
            using SolidBrush reasonBrush = new SolidBrush(UiTheme.TextSecondary);

            g.DrawString(
                isVictory ? "VICTORY!" : "DEFEAT",
                UiTheme.FontHero,
                titleBrush,
                new Rectangle(card.X + 40, card.Y + 26, card.Width - 80, 54),
                UiTheme.CenterFormat);

            g.DrawString(
                reason,
                UiTheme.FontSection,
                reasonBrush,
                new Rectangle(card.X + 60, card.Y + 86, card.Width - 120, 44),
                UiTheme.CenterFormat);
        }

        private static void DrawRunSummaryStats(Graphics g, UiRenderState state, Rectangle card, ActiveRunState? run)
        {
            string heroName = state.Engine.GetCurrentRunCharacter().Name;
            int wave = run?.CurrentWave ?? 1;
            int turns = run?.TurnNumber ?? 0;
            int kills = run?.EnemiesDefeatedInRun ?? 0;
            int coins = run?.CoinsEarnedInRun ?? 0;
            string rerollText = run != null && !run.UsedRerollInRun ? "0 (Clean Run)" : "Used";

            (string Label, string Value, Color Color)[] pills =
            {
            ("HERO", heroName, UiTheme.ShieldCyan),
            ("WAVE REACHED", $"{wave} / 10", UiTheme.AccentGold),
            ("TURNS SURVIVED", turns.ToString(), UiTheme.TextPrimary),
            ("ENEMIES DEFEATED", kills.ToString(), Color.FromArgb(246, 134, 134)),
            ("COINS EARNED", $"+{coins} c", UiTheme.AccentGold),
            ("REROLLS", rerollText, run != null && !run.UsedRerollInRun ? UiTheme.PoisonGreen : UiTheme.TextSecondary)
        };

            int pillW = 228;
            int pillH = 62;
            int startX = card.X + 42;
            int startY = card.Y + 156;

            for (int i = 0; i < pills.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int x = startX + (col * (pillW + 16));
                int y = startY + (row * (pillH + 16));
                UiPrimitives.DrawStatPill(g, new Rectangle(x, y, pillW, pillH), pills[i].Label, pills[i].Value, pills[i].Color);
            }

            if (run != null && run.IsVictory)
            {
                Rectangle unlockBox = new Rectangle(card.X + 42, card.Y + 324, card.Width - 84, 54);
                UiPrimitives.FillRoundedRect(g, unlockBox, 8, Color.FromArgb(30, 66, 46), UiTheme.PoisonGreen, 1.5f);
                using SolidBrush unlockBrush = new SolidBrush(UiTheme.TextPrimary);
                g.DrawString(
                    "Level 1 Conquered! Area 2 is now unlocked in the Main Menu.",
                    UiTheme.FontBodyBold,
                    unlockBrush,
                    unlockBox,
                    UiTheme.CenterFormat);
            }
        }

        private static void DrawActionButtons(Graphics g, UiRenderState state, Rectangle card)
        {
            int btnY = card.Bottom - 84;
            int btnW = 224;
            int btnH = 48;

            UiPrimitives.DrawButton(
                g,
                state,
                new Rectangle(card.X + 42, btnY, btnW, btnH),
                "Play Again",
                "NewRun",
                accentTop: UiTheme.AccentGreenTop,
                accentBottom: UiTheme.AccentGreenBottom);

            UiPrimitives.DrawButton(
                g,
                state,
                new Rectangle(card.X + 288, btnY, btnW, btnH),
                "Achievements & Stats",
                "OpenStats",
                accentTop: UiTheme.AccentPurpleTop,
                accentBottom: UiTheme.AccentPurpleBottom);

            UiPrimitives.DrawButton(
                g,
                state,
                new Rectangle(card.X + 534, btnY, btnW, btnH),
                "Main Menu",
                "BackToMenu",
                accentTop: UiTheme.AccentSlateTop,
                accentBottom: UiTheme.AccentSlateBottom);
        }
    }
}