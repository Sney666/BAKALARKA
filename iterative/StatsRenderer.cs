#nullable disable

namespace RogueCards
{
    public static class StatsRenderer
    {
        private static readonly Rectangle BackButtonRect = new(24, 13, 176, 38);

        public static bool IsBackButtonHit(Point point) => BackButtonRect.Contains(point);

        public static void Render(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            AchievementManager achievementManager,
            Point mouseLocation)
        {
            UiHelpers.DrawBackground(g, UiTheme.BgBottom);
            DrawTopBar(g, database, profile, mouseLocation);
            DrawStatisticsSection(g, achievementManager);
            DrawAchievementsGrid(g, database, achievementManager);
        }

        private static void DrawTopBar(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            Point mouseLocation)
        {
            UiHelpers.DrawHeaderBar(g, 64);

            bool hoverBack = BackButtonRect.Contains(mouseLocation);
            UiHelpers.DrawRoundedBox(
                g,
                BackButtonRect,
                8,
                hoverBack ? Color.FromArgb(52, 65, 86) : Color.FromArgb(37, 46, 61),
                hoverBack ? UiTheme.Gold : UiTheme.Border,
                1.5f);

            using Font btnFont = new("Segoe UI", 10f, FontStyle.Bold);
            using Font titleFont = new("Segoe UI", 17f, FontStyle.Bold);
            using Font pillFont = new("Segoe UI", 10f, FontStyle.Bold);
            using SolidBrush whiteBrush = new(UiTheme.TextPrimary);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush cyanBrush = new(UiTheme.Cyan);
            using StringFormat centerSf = UiHelpers.CreateStringFormat();

            g.DrawString("< Back to Menu", btnFont, whiteBrush, BackButtonRect, centerSf);
            g.DrawString("ACHIEVEMENTS & LIFETIME STATISTICS", titleFont, goldBrush, new Rectangle(220, 10, 620, 44), centerSf);

            Rectangle progPill = new(872, 13, 200, 38);
            UiHelpers.DrawRoundedBox(g, progPill, 8, Color.FromArgb(30, 38, 52), UiTheme.Cyan, 1.3f);
            g.DrawString($"Achievements: {profile.CompletedAchievementIds.Count} / {database.AchievementList.Count}", pillFont, cyanBrush, progPill, centerSf);

            Rectangle coinPill = new(1086, 13, 170, 38);
            UiHelpers.DrawRoundedBox(g, coinPill, 8, Color.FromArgb(38, 34, 24), UiTheme.Gold, 1.3f);
            EntityGraphics.DrawCoinIcon(g, coinPill.X + 20, coinPill.Y + 19, 9);
            g.DrawString($"{profile.Coins} Coins", pillFont, goldBrush, coinPill with { X = coinPill.X + 28, Width = coinPill.Width - 34 }, centerSf);
        }

        private static void DrawStatisticsSection(Graphics g, AchievementManager achievementManager)
        {
            GameStatistics stats = achievementManager.Stats;
            Rectangle panelRect = new(24, 76, 1232, 184);
            UiHelpers.DrawRoundedBox(g, panelRect, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using (Font headerFont = new("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            {
                g.DrawString("LIFETIME COMBAT STATISTICS", headerFont, goldBrush, panelRect.X + 16, panelRect.Y + 10);
            }

            int startX = panelRect.X + 14;
            int row1Y = panelRect.Y + 36;
            DrawPrimaryKpiRow(g, stats, startX, row1Y);
            DrawSecondaryKpiRow(g, stats, achievementManager, startX, row1Y + 70, panelRect.Right - 14);
        }

        private static void DrawPrimaryKpiRow(Graphics g, GameStatistics stats, int startX, int rowY)
        {
            string[] labels = ["RUNS PLAYED", "RUNS WON", "HIGHEST WAVE", "TURNS TAKEN", "ENEMIES DEFEATED", "DAMAGE DEALT"];
            string[] values =
            [
                stats.TotalRunsPlayed.ToString(),
                stats.RunsWon.ToString(),
                $"{stats.HighestWaveReached} / 10",
                stats.TotalTurnsTaken.ToString(),
                stats.TotalEnemiesDefeated.ToString(),
                stats.TotalDamageDealt.ToString()
            ];

            for (int i = 0; i < labels.Length; i++)
            {
                Rectangle box = new(startX + (i * 202), rowY, 192, 60);
                DrawStatBox(g, box, labels[i], values[i], UiTheme.Cyan);
            }
        }

        private static void DrawSecondaryKpiRow(
            Graphics g,
            GameStatistics stats,
            AchievementManager achievementManager,
            int startX,
            int row2Y,
            int panelRight)
        {
            const int smallW = 168;
            const int gap = 10;
            const int row2H = 64;

            DrawStatBox(g, new Rectangle(startX, row2Y, smallW, row2H), "COINS EARNED", stats.TotalCoinsEarned.ToString(), UiTheme.Gold);
            DrawStatBox(g, new Rectangle(startX + smallW + gap, row2Y, smallW, row2H), "CARDS PLAYED", stats.TotalCardsPlayed.ToString(), UiTheme.TextPrimary);
            DrawStatBox(g, new Rectangle(startX + ((smallW + gap) * 2), row2Y, 214, row2H), "MOST USED WEAPON", achievementManager.GetMostUsedWeaponDisplay(), UiTheme.Gold);
            DrawStatBox(
                g,
                new Rectangle(startX + ((smallW + gap) * 2) + 224, row2Y, 198, row2H),
                "FF KILLS / TRAP STUNS",
                $"{stats.TotalFriendlyFireKills}  /  {stats.BearTrapStunsTriggered}",
                UiTheme.Green);

            int breakdownX = startX + ((smallW + gap) * 2) + 432;
            Rectangle breakdownRect = new(breakdownX, row2Y, panelRight - breakdownX, row2H);
            DrawEnemyBreakdownBox(g, stats, breakdownRect);
        }

        private static void DrawEnemyBreakdownBox(Graphics g, GameStatistics stats, Rectangle rect)
        {
            UiHelpers.DrawGradientRoundedBox(g, rect, 7, Color.FromArgb(26, 33, 46), Color.FromArgb(20, 25, 35), Color.FromArgb(48, 58, 76), 1.2f);

            int pawns = stats.EnemiesDefeatedByType.GetValueOrDefault(EnemyType.Pawn);
            int archers = stats.EnemiesDefeatedByType.GetValueOrDefault(EnemyType.Archer);
            int spearmen = stats.EnemiesDefeatedByType.GetValueOrDefault(EnemyType.Spearman);
            int shamans = stats.EnemiesDefeatedByType.GetValueOrDefault(EnemyType.Shaman);
            int bosses = stats.EnemiesDefeatedByType.GetValueOrDefault(EnemyType.VikingChief);

            using Font labelFont = new("Segoe UI", 7.8f, FontStyle.Bold);
            using Font valFont = new("Segoe UI", 8.8f, FontStyle.Bold);
            using SolidBrush mutedBrush = new(UiTheme.TextSecondary);
            using SolidBrush primaryBrush = new(UiTheme.TextPrimary);
            using StringFormat centerSf = UiHelpers.CreateStringFormat();

            g.DrawString("DEFEATED BY ENEMY TYPE", labelFont, mutedBrush, rect with { Y = rect.Y + 6, Height = 16 }, centerSf);
            string breakdownText = $"Pawn: {pawns}  |  Archer: {archers}  |  Spearman: {spearmen}  |  Shaman: {shamans}  |  Boss: {bosses}";
            g.DrawString(breakdownText, valFont, primaryBrush, new Rectangle(rect.X + 8, rect.Y + 26, rect.Width - 16, 30), centerSf);
        }

        private static void DrawAchievementsGrid(
            Graphics g,
            GameDatabase database,
            AchievementManager achievementManager)
        {
            Rectangle panelRect = new(24, 272, 1232, 432);
            UiHelpers.DrawRoundedBox(g, panelRect, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using (Font headerFont = new("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            {
                g.DrawString("ACHIEVEMENTS (2x5 COMPACT GRID)", headerFont, goldBrush, panelRect.X + 16, panelRect.Y + 10);
            }

            List<AchievementDefinition> list = database.AchievementList;
            const int cols = 5;
            const int cardW = 232;
            const int cardH = 182;

            for (int i = 0; i < list.Count && i < 10; i++)
            {
                Rectangle cardRect = new(
                    panelRect.X + 14 + ((i % cols) * (cardW + 11)),
                    panelRect.Y + 40 + ((i / cols) * (cardH + 12)),
                    cardW,
                    cardH);

                AchievementDefinition ach = list[i];
                DrawAchievementCard(g, ach, cardRect, achievementManager.IsAchievementUnlocked(ach.Id), achievementManager.GetAchievementProgress(ach.Id));
            }
        }

        private static void DrawAchievementCard(
            Graphics g,
            AchievementDefinition ach,
            Rectangle rect,
            bool unlocked,
            int progress)
        {
            Color topBg = unlocked ? Color.FromArgb(42, 58, 52) : Color.FromArgb(37, 45, 60);
            Color botBg = unlocked ? Color.FromArgb(28, 39, 36) : Color.FromArgb(26, 32, 44);
            UiHelpers.DrawGradientRoundedBox(g, rect, 9, topBg, botBg, unlocked ? UiTheme.Gold : UiTheme.Border, unlocked ? 2f : 1.3f);
            EntityGraphics.DrawTrophyIcon(g, rect.X + 20, rect.Y + 21, 11, unlocked);

            using Font titleFont = new("Segoe UI", 9.6f, FontStyle.Bold);
            using Font badgeFont = new("Segoe UI", 7.2f, FontStyle.Bold);
            using Font descFont = new("Segoe UI", 8.2f, FontStyle.Regular);
            using Font progFont = new("Segoe UI", 8f, FontStyle.Bold);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush whiteBrush = new(UiTheme.TextPrimary);
            using SolidBrush mutedBrush = new(UiTheme.TextSecondary);
            using SolidBrush greenBrush = new(Color.FromArgb(132, 232, 166));
            using StringFormat centerSf = UiHelpers.CreateStringFormat();

            Rectangle statusRect = new(rect.Right - 76, rect.Y + 10, 66, 18);
            Color statusBg = unlocked ? Color.FromArgb(42, 118, 72) : Color.FromArgb(46, 54, 68);
            UiHelpers.DrawRoundedBox(g, statusRect, 4, statusBg, statusBg, 0f);
            g.DrawString(unlocked ? "UNLOCKED" : "LOCKED", badgeFont, unlocked ? whiteBrush : mutedBrush, statusRect, centerSf);

            g.DrawString(ach.Title, titleFont, unlocked ? goldBrush : whiteBrush, new Rectangle(rect.X + 38, rect.Y + 11, rect.Width - 118, 22));
            g.DrawString(ach.Description, descFont, unlocked ? whiteBrush : mutedBrush, new Rectangle(rect.X + 12, rect.Y + 42, rect.Width - 24, 58));

            DrawAchievementProgressBar(g, ach, rect, unlocked, progress, progFont, whiteBrush, centerSf);

            Rectangle rewardRect = new(rect.X + 10, rect.Bottom - 44, rect.Width - 20, 34);
            UiHelpers.DrawRoundedBox(g, rewardRect, 6, Color.FromArgb(22, 27, 37), Color.FromArgb(46, 56, 73), 1f);
            string rewardLabel = string.IsNullOrEmpty(ach.UnlockCharacterId)
                ? $"Reward: +{ach.CoinReward} Coins"
                : $"Reward: +{ach.CoinReward} Coins + Shieldmaiden";
            g.DrawString(rewardLabel, badgeFont, unlocked ? greenBrush : goldBrush, rewardRect, centerSf);
        }

        private static void DrawAchievementProgressBar(
            Graphics g,
            AchievementDefinition ach,
            Rectangle rect,
            bool unlocked,
            int progress,
            Font progFont,
            Brush whiteBrush,
            StringFormat centerSf)
        {
            int clamped = Math.Min(ach.TargetValue, Math.Max(0, progress));
            float ratio = ach.TargetValue > 0 ? Math.Clamp((float)clamped / ach.TargetValue, 0f, 1f) : 0f;

            Rectangle barBg = new(rect.X + 12, rect.Y + 108, rect.Width - 24, 16);
            UiHelpers.DrawRoundedBox(g, barBg, 4, Color.FromArgb(20, 25, 34), Color.FromArgb(52, 62, 80), 1f);

            if (ratio > 0f)
            {
                int fillW = Math.Max(6, (int)(barBg.Width * ratio));
                Rectangle barFill = new(barBg.X + 1, barBg.Y + 1, fillW - 2, barBg.Height - 2);
                Color barColor = unlocked ? UiTheme.Green : UiTheme.Cyan;
                UiHelpers.DrawRoundedBox(g, barFill, 3, barColor, barColor, 0f);
            }

            g.DrawString($"{clamped} / {ach.TargetValue}", progFont, whiteBrush, barBg, centerSf);
        }

        private static void DrawStatBox(Graphics g, Rectangle rect, string label, string value, Color valueColor)
        {
            UiHelpers.DrawGradientRoundedBox(g, rect, 7, Color.FromArgb(26, 33, 46), Color.FromArgb(20, 25, 35), Color.FromArgb(48, 58, 76), 1.2f);

            using Font labelFont = new("Segoe UI", 7.6f, FontStyle.Bold);
            using Font valFont = new("Segoe UI", 11.5f, FontStyle.Bold);
            using SolidBrush mutedBrush = new(UiTheme.TextSecondary);
            using SolidBrush valBrush = new(valueColor);
            using StringFormat centerSf = UiHelpers.CreateStringFormat();

            g.DrawString(label, labelFont, mutedBrush, new Rectangle(rect.X + 4, rect.Y + 6, rect.Width - 8, 16), centerSf);
            g.DrawString(value, valFont, valBrush, new Rectangle(rect.X + 4, rect.Y + 24, rect.Width - 8, rect.Height - 28), centerSf);
        }
    }
}