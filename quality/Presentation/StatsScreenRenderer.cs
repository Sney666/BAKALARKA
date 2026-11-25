using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using RogueCards.Application;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public sealed class StatsScreenRenderer : IScreenRenderer
    {
        public void Render(Graphics g, UiRenderState state)
        {
            UiPrimitives.DrawScreenBackground(g);
            UiPrimitives.DrawTopHeaderBar(g, "ACHIEVEMENTS & STATISTICS", "Lifetime Combat Record and Milestone Progress");
            UiPrimitives.DrawButton(
            g,
            state,
            new Rectangle(1060, 16, 188, 42),
            "Back to Menu",
            "BackToMenu",
            accentTop: UiTheme.AccentSlateTop,
            accentBottom: UiTheme.AccentSlateBottom);

            DrawLifetimeStats(g, state.Engine);
            DrawAchievementsGrid(g, state.Engine);
            UiPrimitives.DrawAchievementToast(g, state.Engine.Achievements.ActiveToast);
        }

        private static void DrawLifetimeStats(Graphics g, GameEngine engine)
        {
            Rectangle panel = new Rectangle(32, 88, 1216, 146);
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);

            using SolidBrush headerBrush = new SolidBrush(UiTheme.AccentGold);
            g.DrawString("LIFETIME STATISTICS", UiTheme.FontSection, headerBrush, panel.X + 16, panel.Y + 10);

            GameStatistics stats = engine.Achievements.Stats;
            int pillY1 = panel.Y + 38;
            int pillY2 = panel.Y + 90;

            (string Label, string Value, Color Color)[] row1 =
            {
            ("RUNS PLAYED", stats.TotalRunsPlayed.ToString(), UiTheme.TextPrimary),
            ("RUNS WON", stats.RunsWon.ToString(), UiTheme.PoisonGreen),
            ("HIGHEST WAVE", $"{stats.HighestWaveReached} / 10", UiTheme.ShieldCyan),
            ("TURNS TAKEN", stats.TotalTurnsTaken.ToString(), UiTheme.TextPrimary),
            ("ENEMIES SLAIN", stats.TotalEnemiesDefeated.ToString(), Color.FromArgb(246, 134, 134)),
            ("COINS EARNED", $"{stats.TotalCoinsEarned} c", UiTheme.AccentGold)
        };

            int colW1 = 190;
            for (int i = 0; i < row1.Length; i++)
            {
                int x = panel.X + 14 + (i * (colW1 + 10));
                UiPrimitives.DrawStatPill(g, new Rectangle(x, pillY1, colW1, 44), row1[i].Label, row1[i].Value, row1[i].Color);
            }

            string favWeapon = ResolveFavoriteWeaponName(engine, stats);
            (string Label, string Value, Color Color)[] row2 =
            {
            ("TOTAL DAMAGE DEALT", stats.TotalDamageDealt.ToString(), Color.FromArgb(246, 134, 134)),
            ("FRIENDLY FIRE KILLS", stats.TotalFriendlyFireKills.ToString(), UiTheme.AccentGold),
            ("BEAR TRAP STUNS", stats.BearTrapStunsTriggered.ToString(), UiTheme.ShieldCyan),
            ("CARDS PLAYED", stats.TotalCardsPlayed.ToString(), UiTheme.TextPrimary),
            ("MOST USED WEAPON", favWeapon, UiTheme.PoisonGreen)
        };

            int colW2 = 230;
            for (int i = 0; i < row2.Length; i++)
            {
                int x = panel.X + 14 + (i * (colW2 + 10));
                UiPrimitives.DrawStatPill(g, new Rectangle(x, pillY2, colW2, 44), row2[i].Label, row2[i].Value, row2[i].Color);
            }
        }

        private static void DrawAchievementsGrid(Graphics g, GameEngine engine)
        {
            Rectangle panel = new Rectangle(32, 248, 1216, 448);
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);

            int doneCount = engine.Profile.CompletedAchievementIds.Count;
            int totalCount = engine.Database.AchievementList.Count;
            using SolidBrush headerBrush = new SolidBrush(UiTheme.AccentGold);
            g.DrawString(
                $"ACHIEVEMENTS ({doneCount} / {totalCount} UNLOCKED)",
                UiTheme.FontSection,
                headerBrush,
                panel.X + 16,
                panel.Y + 12);

            const int cols = 5;
            const int gapX = 12;
            const int gapY = 12;
            int cardW = (panel.Width - 32 - ((cols - 1) * gapX)) / cols;
            int cardH = 188;

            IReadOnlyList<AchievementDefinition> list = engine.Database.AchievementList;
            for (int i = 0; i < list.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                int x = panel.X + 16 + (col * (cardW + gapX));
                int y = panel.Y + 44 + (row * (cardH + gapY));

                DrawAchievementCard(g, engine, list[i], new Rectangle(x, y, cardW, cardH));
            }
        }

        private static void DrawAchievementCard(
            Graphics g,
            GameEngine engine,
            AchievementDefinition def,
            Rectangle rect)
        {
            bool completed = engine.Achievements.IsCompleted(def.Id);
            int progress = engine.Achievements.GetProgress(def.Id);

            Color fill = completed ? Color.FromArgb(30, 46, 38) : UiTheme.CardBg;
            Color border = completed ? UiTheme.BorderGold : UiTheme.BorderSubtle;
            UiPrimitives.FillRoundedRect(g, rect, 8, fill, border, completed ? 2f : 1.3f);

            Rectangle topBar = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 30);
            UiPrimitives.FillRoundedRect(
                g,
                topBar,
                6,
                completed ? Color.FromArgb(52, 92, 58) : Color.FromArgb(36, 44, 62));

            using SolidBrush titleBrush = new SolidBrush(completed ? UiTheme.AccentGold : UiTheme.TextPrimary);
            using SolidBrush descBrush = new SolidBrush(completed ? UiTheme.TextPrimary : UiTheme.TextSecondary);
            using SolidBrush rewardBrush = new SolidBrush(UiTheme.AccentGold);

            g.DrawString(
                def.Name,
                UiTheme.FontSmallBold,
                titleBrush,
                new Rectangle(topBar.X + 8, topBar.Y, topBar.Width - 16, topBar.Height),
                UiTheme.LeftCenterFormat);

            g.DrawString(
                def.Description,
                UiTheme.FontSmall,
                descBrush,
                new Rectangle(rect.X + 10, rect.Y + 38, rect.Width - 20, 74),
                UiTheme.TopLeftWrapFormat);

            string rewardText = string.IsNullOrWhiteSpace(def.UnlockCharacterId)
                ? $"Reward: +{def.CoinReward} Coins"
                : $"Reward: +{def.CoinReward}c + Shieldmaiden";
            g.DrawString(
                rewardText,
                UiTheme.FontBadge,
                rewardBrush,
                new Rectangle(rect.X + 10, rect.Bottom - 56, rect.Width - 20, 18),
                UiTheme.LeftCenterFormat);

            DrawProgressBar(g, new Rectangle(rect.X + 10, rect.Bottom - 32, rect.Width - 20, 22), progress, def.TargetValue, completed);
        }

        private static void DrawProgressBar(Graphics g, Rectangle rect, int current, int target, bool completed)
        {
            UiPrimitives.FillRoundedRect(g, rect, 5, Color.FromArgb(18, 22, 30), UiTheme.BorderSubtle, 1f);

            float ratio = target > 0 ? Math.Clamp((float)current / target, 0f, 1f) : 0f;
            int fillW = (int)(rect.Width * ratio);
            if (fillW > 2)
            {
                Rectangle fillRect = new Rectangle(rect.X, rect.Y, fillW, rect.Height);
                Color barColor = completed ? UiTheme.AccentGreenTop : UiTheme.AccentBlueTop;
                UiPrimitives.FillRoundedRect(g, fillRect, 5, barColor);
            }

            string text = completed ? "COMPLETED" : $"{current} / {target}";
            using SolidBrush textBrush = new SolidBrush(UiTheme.TextPrimary);
            g.DrawString(text, UiTheme.FontBadge, textBrush, rect, UiTheme.CenterFormat);
        }

        private static string ResolveFavoriteWeaponName(GameEngine engine, GameStatistics stats)
        {
            if (stats.WeaponUseCounts.Count == 0)
            {
                return "None yet";
            }

            KeyValuePair<string, int> top = stats.WeaponUseCounts
                .OrderByDescending(kvp => kvp.Value)
                .First();

            if (engine.Database.Weapons.TryGetValue(top.Key, out WeaponDefinition? weapon))
            {
                return $"{weapon.Name} ({top.Value})";
            }

            return $"{top.Key} ({top.Value})";
        }
    }
}