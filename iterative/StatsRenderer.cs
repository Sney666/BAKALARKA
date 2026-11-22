#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RogueCards
{
    public class StatsRenderer
    {
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;

        private Rectangle backButtonRect = new Rectangle(24, 13, 176, 38);

        private readonly Color colorBgTop = Color.FromArgb(15, 19, 26);
        private readonly Color colorBgBottom = Color.FromArgb(23, 29, 40);
        private readonly Color colorHeader = Color.FromArgb(20, 25, 34);
        private readonly Color colorPanel = Color.FromArgb(27, 33, 44);
        private readonly Color colorCardBg = Color.FromArgb(34, 42, 56);
        private readonly Color colorBorder = Color.FromArgb(58, 70, 90);
        private readonly Color colorGold = Color.FromArgb(228, 174, 62);
        private readonly Color colorCyan = Color.FromArgb(72, 189, 214);
        private readonly Color colorGreen = Color.FromArgb(62, 166, 107);
        private readonly Color colorTextPrimary = Color.FromArgb(238, 242, 248);
        private readonly Color colorTextSecondary = Color.FromArgb(157, 170, 192);
        private readonly Color colorMuted = Color.FromArgb(108, 120, 140);

        public bool IsBackButtonHit(Point point)
        {
            return backButtonRect.Contains(point);
        }

        public void Render(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            AchievementManager achievementManager,
            Point mouseLocation)
        {
            DrawBackground(g);
            DrawTopBar(g, database, profile, mouseLocation);
            DrawStatisticsSection(g, achievementManager);
            DrawAchievementsGrid(g, database, achievementManager);
        }

        private void DrawBackground(Graphics g)
        {
            Rectangle fullRect = new Rectangle(0, 0, WindowWidth, WindowHeight);
            using (LinearGradientBrush brush = new LinearGradientBrush(
                fullRect,
                colorBgTop,
                colorBgBottom,
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, fullRect);
            }
        }

        private void DrawTopBar(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            Point mouseLocation)
        {
            Rectangle barRect = new Rectangle(0, 0, WindowWidth, 64);
            using (SolidBrush barBrush = new SolidBrush(colorHeader))
            {
                g.FillRectangle(barBrush, barRect);
            }

            using (Pen sepPen = new Pen(colorBorder, 1.5f))
            {
                g.DrawLine(sepPen, 0, 64, WindowWidth, 64);
            }

            bool hoverBack = backButtonRect.Contains(mouseLocation);
            DrawRoundedBox(
                g,
                backButtonRect,
                8,
                hoverBack ? Color.FromArgb(52, 65, 86) : Color.FromArgb(37, 46, 61),
                hoverBack ? colorGold : colorBorder,
                1.5f);

            using (Font btnFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Font titleFont = new Font("Segoe UI", 17f, FontStyle.Bold))
            using (Font pillFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush whiteBrush = new SolidBrush(colorTextPrimary))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (StringFormat centerSf = CreateCenterFormat())
            {
                g.DrawString("< Back to Menu", btnFont, whiteBrush, backButtonRect, centerSf);

                Rectangle titleRect = new Rectangle(220, 10, 620, 44);
                g.DrawString("ACHIEVEMENTS & LIFETIME STATISTICS", titleFont, goldBrush, titleRect, centerSf);

                int unlockedCount = profile.CompletedAchievementIds.Count;
                int totalCount = database.AchievementList.Count;
                Rectangle progPill = new Rectangle(872, 13, 200, 38);
                DrawRoundedBox(g, progPill, 8, Color.FromArgb(30, 38, 52), colorCyan, 1.3f);
                g.DrawString($"Achievements: {unlockedCount} / {totalCount}", pillFont, cyanBrush, progPill, centerSf);

                Rectangle coinPill = new Rectangle(1086, 13, 170, 38);
                DrawRoundedBox(g, coinPill, 8, Color.FromArgb(38, 34, 24), colorGold, 1.3f);
                g.DrawString($"{profile.Coins} Coins", pillFont, goldBrush, coinPill, centerSf);
            }
        }

        private void DrawStatisticsSection(Graphics g, AchievementManager achievementManager)
        {
            GameStatistics stats = achievementManager.Stats;
            Rectangle panelRect = new Rectangle(24, 76, 1232, 184);
            DrawRoundedBox(g, panelRect, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            {
                g.DrawString("LIFETIME COMBAT STATISTICS", headerFont, goldBrush, panelRect.X + 16, panelRect.Y + 10);
            }

            string[] row1Labels = new string[]
            {
                "RUNS PLAYED",
                "RUNS WON",
                "HIGHEST WAVE",
                "TURNS TAKEN",
                "ENEMIES DEFEATED",
                "DAMAGE DEALT"
            };

            string[] row1Values = new string[]
            {
                stats.TotalRunsPlayed.ToString(),
                stats.RunsWon.ToString(),
                $"{stats.HighestWaveReached} / 10",
                stats.TotalTurnsTaken.ToString(),
                stats.TotalEnemiesDefeated.ToString(),
                stats.TotalDamageDealt.ToString()
            };

            int kpiW = 192;
            int kpiH = 60;
            int gap = 10;
            int startX = panelRect.X + 14;
            int row1Y = panelRect.Y + 36;

            for (int i = 0; i < row1Labels.Length; i++)
            {
                Rectangle box = new Rectangle(startX + (i * (kpiW + gap)), row1Y, kpiW, kpiH);
                DrawStatBox(g, box, row1Labels[i], row1Values[i], colorCyan);
            }

            int row2Y = row1Y + kpiH + 10;
            int smallW = 168;
            int row2H = 64;

            DrawStatBox(g, new Rectangle(startX, row2Y, smallW, row2H), "COINS EARNED", stats.TotalCoinsEarned.ToString(), colorGold);
            DrawStatBox(g, new Rectangle(startX + (smallW + gap), row2Y, smallW, row2H), "CARDS PLAYED", stats.TotalCardsPlayed.ToString(), colorTextPrimary);
            DrawStatBox(g, new Rectangle(startX + ((smallW + gap) * 2), row2Y, 214, row2H), "MOST USED WEAPON", achievementManager.GetMostUsedWeaponDisplay(), colorGold);
            DrawStatBox(
                g,
                new Rectangle(startX + ((smallW + gap) * 2) + 224, row2Y, 198, row2H),
                "FF KILLS / TRAP STUNS",
                $"{stats.TotalFriendlyFireKills}  /  {stats.BearTrapStunsTriggered}",
                colorGreen);

            int breakdownX = startX + ((smallW + gap) * 2) + 432;
            int breakdownW = (panelRect.Right - 14) - breakdownX;
            Rectangle breakdownRect = new Rectangle(breakdownX, row2Y, breakdownW, row2H);
            DrawRoundedBox(g, breakdownRect, 7, Color.FromArgb(22, 28, 39), Color.FromArgb(48, 58, 76), 1.2f);

            int pawns = GetEnemyCount(stats, EnemyType.Pawn);
            int archers = GetEnemyCount(stats, EnemyType.Archer);
            int spearmen = GetEnemyCount(stats, EnemyType.Spearman);
            int shamans = GetEnemyCount(stats, EnemyType.Shaman);
            int bosses = GetEnemyCount(stats, EnemyType.VikingChief);

            using (Font labelFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (Font valFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            using (SolidBrush primaryBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat centerSf = CreateCenterFormat())
            {
                g.DrawString("DEFEATED BY ENEMY TYPE", labelFont, mutedBrush, new Rectangle(breakdownRect.X, breakdownRect.Y + 6, breakdownRect.Width, 16), centerSf);
                string breakdownText = $"Pawn: {pawns}  |  Archer: {archers}  |  Spearman: {spearmen}  |  Shaman: {shamans}  |  Boss: {bosses}";
                g.DrawString(breakdownText, valFont, primaryBrush, new Rectangle(breakdownRect.X + 8, breakdownRect.Y + 26, breakdownRect.Width - 16, 30), centerSf);
            }
        }

        private void DrawAchievementsGrid(
            Graphics g,
            GameDatabase database,
            AchievementManager achievementManager)
        {
            Rectangle panelRect = new Rectangle(24, 272, 1232, 432);
            DrawRoundedBox(g, panelRect, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            {
                g.DrawString("ACHIEVEMENTS (2x5 COMPACT GRID)", headerFont, goldBrush, panelRect.X + 16, panelRect.Y + 10);
            }

            List<AchievementDefinition> list = database.AchievementList;
            int cols = 5;
            int cardW = 232;
            int cardH = 182;
            int gapX = 11;
            int gapY = 12;
            int startX = panelRect.X + 14;
            int startY = panelRect.Y + 40;

            for (int i = 0; i < list.Count && i < 10; i++)
            {
                int row = i / cols;
                int col = i % cols;
                Rectangle cardRect = new Rectangle(
                    startX + (col * (cardW + gapX)),
                    startY + (row * (cardH + gapY)),
                    cardW,
                    cardH);

                AchievementDefinition ach = list[i];
                bool unlocked = achievementManager.IsAchievementUnlocked(ach.Id);
                int progress = achievementManager.GetAchievementProgress(ach.Id);

                DrawAchievementCard(g, ach, cardRect, unlocked, progress);
            }
        }

        private void DrawAchievementCard(
            Graphics g,
            AchievementDefinition ach,
            Rectangle rect,
            bool unlocked,
            int progress)
        {
            Color bg = unlocked ? Color.FromArgb(36, 48, 44) : colorCardBg;
            Color border = unlocked ? colorGold : colorBorder;
            DrawRoundedBox(g, rect, 9, bg, border, unlocked ? 2f : 1.3f);

            DrawTrophyBadge(g, rect.X + 20, rect.Y + 22, unlocked);

            using (Font titleFont = new Font("Segoe UI", 9.6f, FontStyle.Bold))
            using (Font badgeFont = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            using (Font descFont = new Font("Segoe UI", 8.2f, FontStyle.Regular))
            using (Font progFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush whiteBrush = new SolidBrush(colorTextPrimary))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(132, 232, 166)))
            using (StringFormat centerSf = CreateCenterFormat())
            {
                Rectangle statusRect = new Rectangle(rect.Right - 76, rect.Y + 10, 66, 18);
                Color statusBg = unlocked ? Color.FromArgb(42, 118, 72) : Color.FromArgb(46, 54, 68);
                DrawRoundedBox(g, statusRect, 4, statusBg, statusBg, 0f);
                g.DrawString(unlocked ? "UNLOCKED" : "LOCKED", badgeFont, unlocked ? whiteBrush : mutedBrush, statusRect, centerSf);

                Rectangle titleRect = new Rectangle(rect.X + 38, rect.Y + 11, rect.Width - 118, 22);
                g.DrawString(ach.Title, titleFont, unlocked ? goldBrush : whiteBrush, titleRect);

                Rectangle descRect = new Rectangle(rect.X + 12, rect.Y + 42, rect.Width - 24, 58);
                g.DrawString(ach.Description, descFont, unlocked ? whiteBrush : mutedBrush, descRect);

                int clamped = Math.Min(ach.TargetValue, Math.Max(0, progress));
                float ratio = ach.TargetValue > 0 ? Math.Clamp((float)clamped / ach.TargetValue, 0f, 1f) : 0f;

                Rectangle barBg = new Rectangle(rect.X + 12, rect.Y + 108, rect.Width - 24, 16);
                DrawRoundedBox(g, barBg, 4, Color.FromArgb(20, 25, 34), Color.FromArgb(52, 62, 80), 1f);
                if (ratio > 0f)
                {
                    int fillW = Math.Max(6, (int)(barBg.Width * ratio));
                    Rectangle barFill = new Rectangle(barBg.X + 1, barBg.Y + 1, fillW - 2, barBg.Height - 2);
                    DrawRoundedBox(g, barFill, 3, unlocked ? colorGreen : colorCyan, unlocked ? colorGreen : colorCyan, 0f);
                }

                g.DrawString($"{clamped} / {ach.TargetValue}", progFont, whiteBrush, barBg, centerSf);

                Rectangle rewardRect = new Rectangle(rect.X + 10, rect.Bottom - 44, rect.Width - 20, 34);
                DrawRoundedBox(g, rewardRect, 6, Color.FromArgb(22, 27, 37), Color.FromArgb(46, 56, 73), 1f);

                string rewardLabel = string.IsNullOrEmpty(ach.UnlockCharacterId)
                    ? $"Reward: +{ach.CoinReward} Coins"
                    : $"Reward: +{ach.CoinReward} Coins + Shieldmaiden";

                g.DrawString(rewardLabel, badgeFont, unlocked ? greenBrush : goldBrush, rewardRect, centerSf);
            }
        }

        private void DrawTrophyBadge(Graphics g, int cx, int cy, bool unlocked)
        {
            Color rim = unlocked ? colorGold : colorMuted;
            Color fill = unlocked ? Color.FromArgb(164, 118, 28) : Color.FromArgb(46, 54, 68);

            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (Pen rimPen = new Pen(rim, 1.5f))
            {
                g.FillEllipse(fillBrush, cx - 10, cy - 10, 20, 20);
                g.DrawEllipse(rimPen, cx - 10, cy - 10, 20, 20);

                Point[] star = new Point[]
                {
                    new Point(cx, cy - 6),
                    new Point(cx + 2, cy - 2),
                    new Point(cx + 6, cy - 1),
                    new Point(cx + 3, cy + 2),
                    new Point(cx + 4, cy + 6),
                    new Point(cx, cy + 3),
                    new Point(cx - 4, cy + 6),
                    new Point(cx - 3, cy + 2),
                    new Point(cx - 6, cy - 1),
                    new Point(cx - 2, cy - 2)
                };
                using (SolidBrush starBrush = new SolidBrush(unlocked ? Color.FromArgb(255, 232, 138) : colorMuted))
                {
                    g.FillPolygon(starBrush, star);
                }
            }
        }

        private void DrawStatBox(Graphics g, Rectangle rect, string label, string value, Color valueColor)
        {
            DrawRoundedBox(g, rect, 7, Color.FromArgb(22, 28, 39), Color.FromArgb(48, 58, 76), 1.2f);

            using (Font labelFont = new Font("Segoe UI", 7.6f, FontStyle.Bold))
            using (Font valFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            using (SolidBrush valBrush = new SolidBrush(valueColor))
            using (StringFormat centerSf = CreateCenterFormat())
            {
                g.DrawString(label, labelFont, mutedBrush, new Rectangle(rect.X + 4, rect.Y + 6, rect.Width - 8, 16), centerSf);
                g.DrawString(value, valFont, valBrush, new Rectangle(rect.X + 4, rect.Y + 24, rect.Width - 8, rect.Height - 28), centerSf);
            }
        }

        private static int GetEnemyCount(GameStatistics stats, EnemyType type)
        {
            if (stats.EnemiesDefeatedByType.TryGetValue(type, out int count))
            {
                return count;
            }

            return 0;
        }

        private static void DrawRoundedBox(
            Graphics g,
            Rectangle bounds,
            int radius,
            Color fillColor,
            Color borderColor,
            float borderWidth)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                int diameter = radius * 2;
                path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();

                if (fillColor.A > 0)
                {
                    using (SolidBrush fillBrush = new SolidBrush(fillColor))
                    {
                        g.FillPath(fillBrush, path);
                    }
                }

                if (borderWidth > 0f && borderColor.A > 0)
                {
                    using (Pen borderPen = new Pen(borderColor, borderWidth))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }
            }
        }

        private static StringFormat CreateCenterFormat()
        {
            return new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
        }
    }
}