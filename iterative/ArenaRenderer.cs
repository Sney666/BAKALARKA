#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace RogueCards
{
    public enum ArenaActionType
    {
        None,
        MoveToTile,
        WaitTurn,
        UseSpecial,
        SaveAndExit
    }

    public class ArenaHitbox
    {
        public Rectangle Bounds { get; set; }
        public ArenaActionType ActionType { get; set; }
        public GridPos TilePos { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class ArenaRenderer
    {
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;
        private const int GridSize = 5;
        private const int TileSize = 78;
        private const int TileGap = 4;

        private readonly Rectangle arenaFrameRect = new Rectangle(424, 66, 432, 432);
        private readonly List<ArenaHitbox> hitboxes = new List<ArenaHitbox>();

        private readonly Color colorBgTop = Color.FromArgb(15, 19, 26);
        private readonly Color colorBgBottom = Color.FromArgb(23, 29, 40);
        private readonly Color colorTopBar = Color.FromArgb(20, 25, 34);
        private readonly Color colorPanel = Color.FromArgb(27, 33, 44);
        private readonly Color colorCardBg = Color.FromArgb(34, 42, 56);
        private readonly Color colorBorder = Color.FromArgb(58, 70, 90);
        private readonly Color colorGold = Color.FromArgb(228, 174, 62);
        private readonly Color colorCyan = Color.FromArgb(72, 189, 214);
        private readonly Color colorRed = Color.FromArgb(212, 74, 74);
        private readonly Color colorGreen = Color.FromArgb(62, 166, 107);
        private readonly Color colorPurple = Color.FromArgb(148, 86, 196);
        private readonly Color colorTextPrimary = Color.FromArgb(238, 242, 248);
        private readonly Color colorTextSecondary = Color.FromArgb(157, 170, 192);
        private readonly Color colorMuted = Color.FromArgb(108, 120, 140);

        public ArenaHitbox HitTest(Point point)
        {
            for (int i = hitboxes.Count - 1; i >= 0; i--)
            {
                if (hitboxes[i].Bounds.Contains(point))
                {
                    return hitboxes[i];
                }
            }

            return null;
        }

        public void Render(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation,
            float specialAnimProgress)
        {
            hitboxes.Clear();

            if (engine == null || engine.CurrentRun == null)
            {
                return;
            }

            DrawBackground(g);
            DrawTopStatusBar(g, engine, profile, mouseLocation);
            DrawLeftTacticalPanel(g, engine);
            DrawArenaGrid(g, engine, mouseLocation, specialAnimProgress);
            DrawRightCombatLogPanel(g, engine);
            DrawBottomDock(g, engine, mouseLocation);
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

        private void DrawTopStatusBar(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation)
        {
            ActiveRunState run = engine.CurrentRun;
            PlayerState player = run.Player;
            CharacterDefinition hero = engine.GetActiveCharacterDefinition();

            Rectangle barRect = new Rectangle(0, 0, WindowWidth, 56);
            using (SolidBrush barBrush = new SolidBrush(colorTopBar))
            {
                g.FillRectangle(barBrush, barRect);
            }

            using (Pen sepPen = new Pen(colorBorder, 1.5f))
            {
                g.DrawLine(sepPen, 0, 56, WindowWidth, 56);
            }

            Rectangle heroPill = new Rectangle(20, 10, 196, 36);
            DrawRoundedBox(g, heroPill, 7, Color.FromArgb(32, 42, 58), colorGold, 1.5f);
            using (Font heroFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString(hero.Name.ToUpperInvariant(), heroFont, goldBrush, heroPill, sf);
            }

            Rectangle hpBox = new Rectangle(230, 10, 214, 36);
            DrawRoundedBox(g, hpBox, 7, Color.FromArgb(30, 36, 48), colorBorder, 1.2f);
            float hpRatio = player.MaxHp > 0 ? Math.Clamp((float)player.CurrentHp / player.MaxHp, 0f, 1f) : 0f;
            Rectangle hpBarBg = new Rectangle(hpBox.X + 10, hpBox.Y + 8, 114, 20);
            DrawRoundedBox(g, hpBarBg, 4, Color.FromArgb(46, 26, 30), Color.FromArgb(95, 42, 46), 1f);
            if (hpRatio > 0f)
            {
                int fillW = Math.Max(8, (int)(hpBarBg.Width * hpRatio));
                Rectangle hpBarFill = new Rectangle(hpBarBg.X + 1, hpBarBg.Y + 1, fillW - 2, hpBarBg.Height - 2);
                DrawRoundedBox(g, hpBarFill, 3, colorRed, colorRed, 0f);
            }

            using (Font statFont = new Font("Segoe UI", 9.8f, FontStyle.Bold))
            using (SolidBrush whiteBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat sf = CreateCenterFormat())
            {
                Rectangle hpLabelRect = new Rectangle(hpBarBg.Right + 6, hpBox.Y, 80, hpBox.Height);
                g.DrawString($"HP {player.CurrentHp}/{player.MaxHp}", statFont, whiteBrush, hpLabelRect, sf);
            }

            Rectangle coinBox = new Rectangle(458, 10, 148, 36);
            DrawRoundedBox(g, coinBox, 7, Color.FromArgb(38, 34, 24), colorGold, 1.2f);
            DrawCoinIcon(g, coinBox.X + 20, coinBox.Y + 18, 9);
            using (Font coinFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (StringFormat leftSf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                g.DrawString($"{profile.Coins} Coins", coinFont, goldBrush, new Rectangle(coinBox.X + 36, coinBox.Y, 106, coinBox.Height), leftSf);
            }

            Rectangle waveBox = new Rectangle(620, 10, 246, 36);
            DrawRoundedBox(g, waveBox, 7, Color.FromArgb(30, 38, 52), colorCyan, 1.2f);
            using (Font waveFont = new Font("Segoe UI", 10.2f, FontStyle.Bold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString($"WAVE {run.CurrentWave} / 10   |   TURN {run.TurnNumber}", waveFont, cyanBrush, waveBox, sf);
            }

            Rectangle upgradeBox = new Rectangle(880, 10, 192, 36);
            int activeUpgrades = engine.GetActiveUpgradeNames().Count;
            DrawRoundedBox(g, upgradeBox, 7, Color.FromArgb(30, 36, 48), colorBorder, 1.2f);
            using (Font upgFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush secondaryBrush = new SolidBrush(activeUpgrades > 0 ? colorGold : colorTextSecondary))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString($"Upgrades Active: {activeUpgrades}", upgFont, secondaryBrush, upgradeBox, sf);
            }

            Rectangle exitBtn = new Rectangle(1086, 10, 174, 36);
            bool hoverExit = exitBtn.Contains(mouseLocation);
            DrawRoundedBox(
                g,
                exitBtn,
                7,
                hoverExit ? Color.FromArgb(58, 70, 92) : Color.FromArgb(38, 46, 61),
                hoverExit ? colorGold : colorBorder,
                1.5f);
            using (Font btnFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush whiteBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString("Save & Exit to Menu", btnFont, whiteBrush, exitBtn, sf);
            }

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = exitBtn,
                ActionType = ArenaActionType.SaveAndExit,
                IsEnabled = true
            });
        }

        private void DrawLeftTacticalPanel(Graphics g, GameEngine engine)
        {
            PlayerState player = engine.CurrentRun.Player;
            CharacterDefinition hero = engine.GetActiveCharacterDefinition();

            Rectangle heroPanel = new Rectangle(20, 66, 384, 176);
            DrawRoundedBox(g, heroPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Font boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font bodyFont = new Font("Segoe UI", 8.8f, FontStyle.Regular))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (SolidBrush primaryBrush = new SolidBrush(colorTextPrimary))
            using (SolidBrush secondaryBrush = new SolidBrush(colorTextSecondary))
            {
                g.DrawString("HERO & ACTIVE BONUSES", headerFont, goldBrush, heroPanel.X + 14, heroPanel.Y + 11);

                g.DrawString(
                    $"Position: ({player.Position.X}, {player.Position.Y})   |   Free Rerolls: {player.FreeRerollsRemaining}",
                    boldFont,
                    cyanBrush,
                    heroPanel.X + 14,
                    heroPanel.Y + 36);

                string specialStatus = player.SpecialCooldown == 0
                    ? "READY"
                    : $"Cooldown: {player.SpecialCooldown} / {player.SpecialMaxCooldown}";

                g.DrawString(
                    $"Special: {hero.SpecialName} ({specialStatus})",
                    boldFont,
                    player.SpecialCooldown == 0 ? goldBrush : secondaryBrush,
                    heroPanel.X + 14,
                    heroPanel.Y + 58);

                Rectangle specialDescRect = new Rectangle(heroPanel.X + 14, heroPanel.Y + 78, heroPanel.Width - 28, 38);
                string specialDesc = engine.GetSpecialDamageBonus() > 0
                    ? $"{hero.SpecialDescription} (+{engine.GetSpecialDamageBonus()} dmg)"
                    : hero.SpecialDescription;
                g.DrawString(specialDesc, bodyFont, secondaryBrush, specialDescRect);

                List<string> bonuses = BuildUpgradeSummaryLines(engine);
                string bonusText = bonuses.Count > 0
                    ? $"Bonuses: {string.Join(" | ", bonuses)}"
                    : "Bonuses: None unlocked yet (buy in Main Menu)";
                Rectangle bonusRect = new Rectangle(heroPanel.X + 14, heroPanel.Y + 122, heroPanel.Width - 28, 44);
                DrawRoundedBox(g, bonusRect, 6, Color.FromArgb(22, 27, 37), Color.FromArgb(46, 56, 73), 1f);
                g.DrawString(bonusText, bodyFont, bonuses.Count > 0 ? primaryBrush : secondaryBrush, new Rectangle(bonusRect.X + 8, bonusRect.Y + 6, bonusRect.Width - 16, bonusRect.Height - 12));
            }

            Rectangle legendPanel = new Rectangle(20, 254, 384, 134);
            DrawRoundedBox(g, legendPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Font itemFont = new Font("Segoe UI", 8.8f, FontStyle.Regular))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush textBrush = new SolidBrush(colorTextPrimary))
            {
                g.DrawString("TACTICAL LEGEND", headerFont, goldBrush, legendPanel.X + 14, legendPanel.Y + 11);

                DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 38, colorCyan, "Adjacent Cyan Tile: Valid Orthogonal Move", itemFont, textBrush);
                DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 62, colorRed, "Pulsing Red Tile: Enemy Charged Attack Telegraph", itemFont, textBrush);
                DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 86, colorGreen, "Green Toxic Glow: Poison Ground Hazard", itemFont, textBrush);
                DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 110, colorGold, "Spiked Ring: Armed Bear Trap Hazard", itemFont, textBrush);
            }

            Rectangle controlsPanel = new Rectangle(20, 400, 384, 98);
            DrawRoundedBox(g, controlsPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Font ctrlFont = new Font("Segoe UI", 8.8f, FontStyle.Regular))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            {
                g.DrawString("CONTROLS", headerFont, goldBrush, controlsPanel.X + 14, controlsPanel.Y + 9);
                g.DrawString("Move (1 Turn): WASD, Arrow Keys, or Click Adjacent Tile", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 33);
                g.DrawString("Wait (1 Turn): Spacebar   |   Special (1 Turn): Key E", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 53);
                g.DrawString("Return to Main Menu: ESC or Save & Exit Button", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 73);
            }
        }

        private void DrawArenaGrid(
            Graphics g,
            GameEngine engine,
            Point mouseLocation,
            float specialAnimProgress)
        {
            DrawRoundedBox(g, arenaFrameRect, 12, Color.FromArgb(22, 27, 37), colorGold, 2f);

            int gridTotalSize = (GridSize * TileSize) + ((GridSize - 1) * TileGap);
            int startX = arenaFrameRect.X + ((arenaFrameRect.Width - gridTotalSize) / 2);
            int startY = arenaFrameRect.Y + ((arenaFrameRect.Height - gridTotalSize) / 2);

            List<GridPos> validMoves = engine.GetValidMoveTiles();
            HashSet<GridPos> validMoveSet = new HashSet<GridPos>(validMoves);

            for (int row = 0; row < GridSize; row++)
            {
                for (int col = 0; col < GridSize; col++)
                {
                    GridPos pos = new GridPos(col, row);
                    Rectangle tileRect = new Rectangle(
                        startX + (col * (TileSize + TileGap)),
                        startY + (row * (TileSize + TileGap)),
                        TileSize,
                        TileSize);

                    bool isCheckerAlt = ((row + col) % 2) == 1;
                    bool isValidMove = validMoveSet.Contains(pos);
                    bool isHovered = tileRect.Contains(mouseLocation);

                    Color tileBg = isCheckerAlt
                        ? Color.FromArgb(35, 43, 57)
                        : Color.FromArgb(30, 37, 49);

                    Color tileBorder = Color.FromArgb(52, 64, 82);
                    float borderWidth = 1.2f;

                    if (isValidMove)
                    {
                        tileBg = isHovered
                            ? Color.FromArgb(42, 78, 98)
                            : Color.FromArgb(33, 52, 68);
                        tileBorder = isHovered ? Color.FromArgb(124, 226, 245) : Color.FromArgb(62, 138, 162);
                        borderWidth = isHovered ? 2.2f : 1.5f;
                    }

                    DrawRoundedBox(g, tileRect, 7, tileBg, tileBorder, borderWidth);

                    if (isValidMove)
                    {
                        DrawMoveDirectionHint(g, tileRect, engine.CurrentRun.Player.Position, pos, isHovered);

                        hitboxes.Add(new ArenaHitbox
                        {
                            Bounds = tileRect,
                            ActionType = ArenaActionType.MoveToTile,
                            TilePos = pos,
                            IsEnabled = true
                        });
                    }
                }
            }

            if (specialAnimProgress > 0f && !string.IsNullOrEmpty(engine.LastSpecialEffectType))
            {
                DrawSpecialAbilityEffect(g, startX, startY, engine.LastSpecialOrigin, engine.LastSpecialEffectType, specialAnimProgress);
            }

            PlayerState player = engine.CurrentRun.Player;
            Rectangle playerTileRect = new Rectangle(
                startX + (player.Position.X * (TileSize + TileGap)),
                startY + (player.Position.Y * (TileSize + TileGap)),
                TileSize,
                TileSize);

            DrawPlayerSpriteOnTile(g, playerTileRect, player, engine.GetActiveCharacterDefinition());
        }

        private void DrawMoveDirectionHint(
            Graphics g,
            Rectangle tileRect,
            GridPos playerPos,
            GridPos targetPos,
            bool isHovered)
        {
            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2);
            int dx = targetPos.X - playerPos.X;
            int dy = targetPos.Y - playerPos.Y;

            Color arrowColor = isHovered
                ? Color.FromArgb(220, 136, 232, 248)
                : Color.FromArgb(95, 72, 189, 214);

            int s = isHovered ? 9 : 7;
            Point[] triangle;

            if (dx == 1)
            {
                triangle = new Point[] { new Point(cx + s, cy), new Point(cx - s + 2, cy - s + 1), new Point(cx - s + 2, cy + s - 1) };
            }
            else if (dx == -1)
            {
                triangle = new Point[] { new Point(cx - s, cy), new Point(cx + s - 2, cy - s + 1), new Point(cx + s - 2, cy + s - 1) };
            }
            else if (dy == -1)
            {
                triangle = new Point[] { new Point(cx, cy - s), new Point(cx - s + 1, cy + s - 2), new Point(cx + s - 1, cy + s - 2) };
            }
            else
            {
                triangle = new Point[] { new Point(cx, cy + s), new Point(cx - s + 1, cy - s + 2), new Point(cx + s - 1, cy - s + 2) };
            }

            using (SolidBrush brush = new SolidBrush(arrowColor))
            {
                g.FillPolygon(brush, triangle);
            }
        }

        private void DrawSpecialAbilityEffect(
            Graphics g,
            int gridStartX,
            int gridStartY,
            GridPos origin,
            string effectType,
            float progress)
        {
            Rectangle originTile = new Rectangle(
                gridStartX + (origin.X * (TileSize + TileGap)),
                gridStartY + (origin.Y * (TileSize + TileGap)),
                TileSize,
                TileSize);

            int cx = originTile.X + (originTile.Width / 2);
            int cy = originTile.Y + (originTile.Height / 2);
            int alpha = Math.Clamp((int)(progress * 210), 0, 210);

            if (effectType == "VikingScream")
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }

                        GridPos adj = origin.Offset(dx, dy);
                        if (!adj.IsValid(GridSize))
                        {
                            continue;
                        }

                        Rectangle adjRect = new Rectangle(
                            gridStartX + (adj.X * (TileSize + TileGap)),
                            gridStartY + (adj.Y * (TileSize + TileGap)),
                            TileSize,
                            TileSize);

                        using (SolidBrush pulseBrush = new SolidBrush(Color.FromArgb(alpha / 2, 236, 184, 62)))
                        {
                            g.FillRectangle(pulseBrush, adjRect);
                        }
                    }
                }

                float expansion = 1f - progress;
                int radius = 26 + (int)(expansion * 96);
                using (Pen ringPen = new Pen(Color.FromArgb(alpha, 245, 200, 86), 4f))
                {
                    g.DrawEllipse(ringPen, cx - radius, cy - radius, radius * 2, radius * 2);
                }
            }
            else if (effectType == "IronBulwark")
            {
                int radius = 34 + (int)((1f - progress) * 18);
                using (SolidBrush domeBrush = new SolidBrush(Color.FromArgb(alpha / 2, 72, 208, 235)))
                using (Pen domePen = new Pen(Color.FromArgb(alpha, 152, 242, 255), 3.5f))
                {
                    g.FillEllipse(domeBrush, cx - radius, cy - radius, radius * 2, radius * 2);
                    g.DrawEllipse(domePen, cx - radius, cy - radius, radius * 2, radius * 2);
                }
            }
        }

        private void DrawPlayerSpriteOnTile(
            Graphics g,
            Rectangle tileRect,
            PlayerState player,
            CharacterDefinition hero)
        {
            DrawRoundedBox(
                g,
                new Rectangle(tileRect.X + 2, tileRect.Y + 2, tileRect.Width - 4, tileRect.Height - 4),
                6,
                Color.FromArgb(45, 58, 92, 134),
                hero.Id == "shieldmaiden" ? colorCyan : colorGold,
                1.8f);

            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2) - 1;

            if (string.Equals(hero.Id, "shieldmaiden", StringComparison.OrdinalIgnoreCase))
            {
                DrawShieldmaidenSprite(g, cx, cy);
            }
            else
            {
                DrawVikingSprite(g, cx, cy);
            }

            using (Font nameFont = new Font("Segoe UI", 7.3f, FontStyle.Bold))
            using (SolidBrush labelBg = new SolidBrush(Color.FromArgb(185, 16, 20, 28)))
            using (SolidBrush whiteBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat sf = CreateCenterFormat())
            {
                Rectangle labelRect = new Rectangle(tileRect.X + 4, tileRect.Y + 3, tileRect.Width - 8, 14);
                g.FillRectangle(labelBg, labelRect);
                g.DrawString(hero.Name, nameFont, whiteBrush, labelRect, sf);
            }

            Rectangle hpBg = new Rectangle(tileRect.X + 8, tileRect.Bottom - 11, tileRect.Width - 16, 7);
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(35, 18, 22)))
            using (Pen borderPen = new Pen(Color.FromArgb(18, 22, 29), 1f))
            {
                g.FillRectangle(bgBrush, hpBg);

                float ratio = player.MaxHp > 0 ? Math.Clamp((float)player.CurrentHp / player.MaxHp, 0f, 1f) : 0f;
                int fillW = (int)(hpBg.Width * ratio);
                if (fillW > 0)
                {
                    using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(72, 204, 122)))
                    {
                        g.FillRectangle(fillBrush, hpBg.X, hpBg.Y, fillW, hpBg.Height);
                    }
                }

                g.DrawRectangle(borderPen, hpBg);
            }
        }

        private void DrawVikingSprite(Graphics g, int cx, int cy)
        {
            using (SolidBrush cloakBrush = new SolidBrush(Color.FromArgb(44, 96, 174)))
            using (Pen goldPen = new Pen(colorGold, 1.5f))
            {
                Point[] body = new Point[]
                {
                    new Point(cx - 17, cy + 21),
                    new Point(cx - 13, cy + 4),
                    new Point(cx + 13, cy + 4),
                    new Point(cx + 17, cy + 21)
                };
                g.FillPolygon(cloakBrush, body);
                g.DrawPolygon(goldPen, body);
            }

            using (SolidBrush skinBrush = new SolidBrush(Color.FromArgb(228, 186, 150)))
            using (SolidBrush beardBrush = new SolidBrush(Color.FromArgb(188, 118, 48)))
            {
                g.FillEllipse(skinBrush, cx - 9, cy - 10, 18, 18);
                Point[] beard = new Point[]
                {
                    new Point(cx - 9, cy),
                    new Point(cx + 9, cy),
                    new Point(cx + 6, cy + 11),
                    new Point(cx, cy + 15),
                    new Point(cx - 6, cy + 11)
                };
                g.FillPolygon(beardBrush, beard);
            }

            using (SolidBrush hornBrush = new SolidBrush(Color.FromArgb(238, 228, 202)))
            {
                Point[] leftHorn = new Point[]
                {
                    new Point(cx - 9, cy - 8),
                    new Point(cx - 21, cy - 14),
                    new Point(cx - 16, cy - 22),
                    new Point(cx - 13, cy - 14),
                    new Point(cx - 7, cy - 12)
                };
                Point[] rightHorn = new Point[]
                {
                    new Point(cx + 9, cy - 8),
                    new Point(cx + 21, cy - 14),
                    new Point(cx + 16, cy - 22),
                    new Point(cx + 13, cy - 14),
                    new Point(cx + 7, cy - 12)
                };
                g.FillPolygon(hornBrush, leftHorn);
                g.FillPolygon(hornBrush, rightHorn);
            }

            using (SolidBrush helmBrush = new SolidBrush(Color.FromArgb(148, 162, 184)))
            using (Pen goldPen = new Pen(colorGold, 1.5f))
            {
                g.FillPie(helmBrush, cx - 11, cy - 18, 22, 18, 180, 180);
                g.DrawArc(goldPen, cx - 11, cy - 18, 22, 18, 180, 180);
                g.DrawLine(goldPen, cx - 11, cy - 9, cx + 11, cy - 9);
            }

            using (Pen shaftPen = new Pen(Color.FromArgb(146, 98, 54), 2.4f))
            using (SolidBrush bladeBrush = new SolidBrush(Color.FromArgb(204, 216, 232)))
            {
                g.DrawLine(shaftPen, cx + 15, cy + 20, cx + 21, cy - 10);
                Point[] blade = new Point[]
                {
                    new Point(cx + 19, cy - 8),
                    new Point(cx + 27, cy - 12),
                    new Point(cx + 26, cy),
                    new Point(cx + 18, cy - 2)
                };
                g.FillPolygon(bladeBrush, blade);
            }
        }

        private void DrawShieldmaidenSprite(Graphics g, int cx, int cy)
        {
            using (SolidBrush armorBrush = new SolidBrush(Color.FromArgb(52, 148, 178)))
            using (Pen silverPen = new Pen(Color.FromArgb(204, 220, 236), 1.5f))
            {
                Point[] body = new Point[]
                {
                    new Point(cx - 16, cy + 21),
                    new Point(cx - 12, cy + 4),
                    new Point(cx + 12, cy + 4),
                    new Point(cx + 16, cy + 21)
                };
                g.FillPolygon(armorBrush, body);
                g.DrawPolygon(silverPen, body);
            }

            using (SolidBrush skinBrush = new SolidBrush(Color.FromArgb(234, 200, 174)))
            {
                g.FillEllipse(skinBrush, cx - 8, cy - 10, 16, 17);
            }

            using (SolidBrush wingBrush = new SolidBrush(Color.FromArgb(208, 224, 240)))
            {
                Point[] leftWing = new Point[]
                {
                    new Point(cx - 9, cy - 8),
                    new Point(cx - 21, cy - 19),
                    new Point(cx - 16, cy - 10),
                    new Point(cx - 9, cy - 6)
                };
                Point[] rightWing = new Point[]
                {
                    new Point(cx + 9, cy - 8),
                    new Point(cx + 21, cy - 19),
                    new Point(cx + 16, cy - 10),
                    new Point(cx + 9, cy - 6)
                };
                g.FillPolygon(wingBrush, leftWing);
                g.FillPolygon(wingBrush, rightWing);
            }

            using (SolidBrush helmBrush = new SolidBrush(Color.FromArgb(196, 212, 228)))
            using (Pen cyanPen = new Pen(colorCyan, 1.5f))
            {
                g.FillPie(helmBrush, cx - 10, cy - 18, 20, 18, 180, 180);
                g.DrawLine(cyanPen, cx - 10, cy - 9, cx + 10, cy - 9);
            }

            Rectangle shieldRect = new Rectangle(cx + 4, cy + 1, 22, 22);
            using (SolidBrush shieldFill = new SolidBrush(Color.FromArgb(28, 58, 76)))
            using (Pen rimPen = new Pen(Color.FromArgb(208, 224, 240), 2f))
            using (Pen runePen = new Pen(colorCyan, 1.4f))
            {
                g.FillEllipse(shieldFill, shieldRect);
                g.DrawEllipse(rimPen, shieldRect);
                g.DrawLine(runePen, shieldRect.X + 11, shieldRect.Y + 4, shieldRect.X + 11, shieldRect.Bottom - 4);
                g.DrawLine(runePen, shieldRect.X + 4, shieldRect.Y + 11, shieldRect.Right - 4, shieldRect.Y + 11);
            }
        }

        private void DrawRightCombatLogPanel(Graphics g, GameEngine engine)
        {
            Rectangle logPanel = new Rectangle(876, 66, 384, 432);
            DrawRoundedBox(g, logPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (Font logFont = new Font("Segoe UI", 8.7f, FontStyle.Regular))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (SolidBrush primaryBrush = new SolidBrush(colorTextPrimary))
            using (SolidBrush secondaryBrush = new SolidBrush(colorTextSecondary))
            {
                g.DrawString("COMBAT LOG (LATEST 8 ENTRIES)", headerFont, goldBrush, logPanel.X + 14, logPanel.Y + 12);

                Rectangle waveSummary = new Rectangle(logPanel.X + 14, logPanel.Y + 38, logPanel.Width - 28, 28);
                DrawRoundedBox(g, waveSummary, 6, Color.FromArgb(22, 28, 38), Color.FromArgb(46, 56, 73), 1f);
                using (StringFormat sf = CreateCenterFormat())
                {
                    int aliveEnemies = engine.CurrentRun.Enemies.Count(e => e.CurrentHp > 0);
                    g.DrawString(
                        $"Wave {engine.CurrentRun.CurrentWave}  |  Enemies: {aliveEnemies}  |  Turn: {engine.CurrentRun.TurnNumber}",
                        subFont,
                        cyanBrush,
                        waveSummary,
                        sf);
                }

                List<string> entries = engine.CurrentRun.RecentLogEntries;
                int maxDisplay = 8;
                int startIndex = Math.Max(0, entries.Count - maxDisplay);
                int rowY = logPanel.Y + 76;
                int rowHeight = 38;

                for (int i = startIndex; i < entries.Count; i++)
                {
                    bool isLatest = (i == entries.Count - 1);
                    Rectangle rowRect = new Rectangle(logPanel.X + 14, rowY, logPanel.Width - 28, rowHeight);
                    Color rowBg = isLatest ? Color.FromArgb(38, 48, 64) : Color.FromArgb(24, 30, 41);
                    Color rowBorder = isLatest ? colorGold : Color.FromArgb(42, 51, 67);
                    DrawRoundedBox(g, rowRect, 6, rowBg, rowBorder, 1f);

                    Rectangle textRect = new Rectangle(rowRect.X + 8, rowRect.Y + 4, rowRect.Width - 16, rowRect.Height - 8);
                    g.DrawString(entries[i], logFont, isLatest ? primaryBrush : secondaryBrush, textRect);

                    rowY += rowHeight + 4;
                }

                if (!string.IsNullOrWhiteSpace(engine.LastActionFeedback))
                {
                    Rectangle feedbackRect = new Rectangle(logPanel.X + 14, logPanel.Bottom - 34, logPanel.Width - 28, 24);
                    g.DrawString(engine.LastActionFeedback, subFont, goldBrush, feedbackRect);
                }
            }
        }

        private void DrawBottomDock(Graphics g, GameEngine engine, Point mouseLocation)
        {
            PlayerState player = engine.CurrentRun.Player;
            CharacterDefinition hero = engine.GetActiveCharacterDefinition();

            Rectangle handPanel = new Rectangle(20, 508, 630, 200);
            DrawRoundedBox(g, handPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            {
                g.DrawString($"HAND ({player.Hand.Count} CARDS)", headerFont, goldBrush, handPanel.X + 14, handPanel.Y + 9);
            }

            int cardWidth = 144;
            int cardHeight = 158;
            int cardGap = 10;
            int cardStartX = handPanel.X + 14;
            int cardY = handPanel.Y + 34;

            for (int i = 0; i < player.Hand.Count && i < 4; i++)
            {
                Rectangle cardRect = new Rectangle(cardStartX + (i * (cardWidth + cardGap)), cardY, cardWidth, cardHeight);
                DrawCompactCard(g, engine, player.Hand[i], cardRect);
            }

            Rectangle queuePanel = new Rectangle(664, 508, 320, 200);
            DrawRoundedBox(g, queuePanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Font slotFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString($"EQUIPPED QUEUE ({player.EquippedQueue.Count} / 3)", headerFont, goldBrush, queuePanel.X + 14, queuePanel.Y + 9);

                int slotW = 92;
                int slotH = 154;
                int slotGap = 8;
                int slotStartX = queuePanel.X + 14;
                int slotY = queuePanel.Y + 36;

                for (int i = 0; i < 3; i++)
                {
                    Rectangle slotRect = new Rectangle(slotStartX + (i * (slotW + slotGap)), slotY, slotW, slotH);
                    if (i < player.EquippedQueue.Count)
                    {
                        DrawCompactCard(g, engine, player.EquippedQueue[i], slotRect);
                    }
                    else
                    {
                        DrawRoundedBox(g, slotRect, 8, Color.FromArgb(22, 27, 36), Color.FromArgb(48, 58, 74), 1.2f);
                        g.DrawString($"SLOT {i + 1}\nEmpty", slotFont, mutedBrush, slotRect, sf);
                    }
                }
            }

            Rectangle actionPanel = new Rectangle(998, 508, 262, 200);
            DrawRoundedBox(g, actionPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            {
                g.DrawString("TURN ACTIONS", headerFont, goldBrush, actionPanel.X + 14, actionPanel.Y + 9);
            }

            Rectangle waitBtn = new Rectangle(actionPanel.X + 14, actionPanel.Y + 36, actionPanel.Width - 28, 44);
            bool hoverWait = waitBtn.Contains(mouseLocation);
            Color waitTop = hoverWait ? Color.FromArgb(66, 86, 114) : Color.FromArgb(46, 60, 82);
            Color waitBottom = hoverWait ? Color.FromArgb(48, 64, 88) : Color.FromArgb(34, 44, 62);
            string waitLabel = engine.GetWaitEquippedCooldownBonus() > 0
                ? "WAIT [Space] (+1 Eq CD)"
                : "WAIT TURN [Space]";
            DrawGradientButton(g, waitBtn, waitTop, waitBottom, hoverWait ? colorCyan : colorBorder, waitLabel, 9.8f, colorTextPrimary);

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = waitBtn,
                ActionType = ArenaActionType.WaitTurn,
                IsEnabled = true
            });

            Rectangle specialBtn = new Rectangle(actionPanel.X + 14, actionPanel.Y + 88, actionPanel.Width - 28, 52);
            bool specialReady = player.SpecialCooldown <= 0;
            bool hoverSpecial = specialBtn.Contains(mouseLocation) && specialReady;

            if (specialReady)
            {
                Color spTop = hero.Id == "shieldmaiden"
                    ? (hoverSpecial ? Color.FromArgb(88, 206, 232) : Color.FromArgb(58, 166, 194))
                    : (hoverSpecial ? Color.FromArgb(238, 184, 72) : Color.FromArgb(206, 150, 42));
                Color spBottom = hero.Id == "shieldmaiden"
                    ? (hoverSpecial ? Color.FromArgb(52, 154, 182) : Color.FromArgb(38, 124, 150))
                    : (hoverSpecial ? Color.FromArgb(192, 136, 30) : Color.FromArgb(162, 112, 24));
                Color spText = hero.Id == "shieldmaiden" ? Color.White : Color.FromArgb(18, 20, 26);

                DrawGradientButton(
                    g,
                    specialBtn,
                    spTop,
                    spBottom,
                    hero.Id == "shieldmaiden" ? colorCyan : colorGold,
                    $"{hero.SpecialName.ToUpperInvariant()} [E]\nREADY (1 Turn)",
                    9.2f,
                    spText);
            }
            else
            {
                DrawRoundedBox(g, specialBtn, 8, Color.FromArgb(32, 37, 48), Color.FromArgb(58, 66, 82), 1.5f);
                using (Font btnFont = new Font("Segoe UI", 9.2f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString(
                        $"{hero.SpecialName.ToUpperInvariant()} [E]\nCOOLDOWN: {player.SpecialCooldown} TURNS",
                        btnFont,
                        mutedBrush,
                        specialBtn,
                        sf);
                }
            }

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = specialBtn,
                ActionType = ArenaActionType.UseSpecial,
                IsEnabled = specialReady
            });

            Rectangle playBtn = new Rectangle(actionPanel.X + 14, actionPanel.Y + 148, actionPanel.Width - 28, 40);
            DrawRoundedBox(g, playBtn, 8, Color.FromArgb(30, 35, 45), Color.FromArgb(52, 60, 74), 1.2f);
            using (Font btnFont = new Font("Segoe UI", 9.2f, FontStyle.Bold))
            using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
            using (StringFormat sf = CreateCenterFormat())
            {
                string playText = player.EquippedQueue.Count > 0
                    ? $"PLAY QUEUE ({player.EquippedQueue.Count})"
                    : "PLAY QUEUE (EMPTY)";
                g.DrawString(playText, btnFont, mutedBrush, playBtn, sf);
            }
        }

        private void DrawCompactCard(Graphics g, GameEngine engine, CardInstance card, Rectangle rect)
        {
            if (card == null || card.Definition == null)
            {
                return;
            }

            WeaponDefinition def = card.Definition;
            Color catColor = GetCategoryColor(def.Category);

            Rectangle shadow = new Rectangle(rect.X + 3, rect.Y + 3, rect.Width, rect.Height);
            DrawRoundedBox(g, shadow, 8, Color.FromArgb(95, 0, 0, 0), Color.Transparent, 0f);
            DrawRoundedBox(g, rect, 8, colorCardBg, card.IsReady ? catColor : colorBorder, 1.5f);

            Rectangle headerRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, 28);
            using (GraphicsPath path = CreateTopRoundedRectPath(headerRect, 6))
            using (SolidBrush headerBrush = new SolidBrush(catColor))
            {
                g.FillPath(headerBrush, path);
            }

            int effectiveDamage = engine.GetEffectiveCardDamage(card);
            int effectiveMaxCd = engine.GetEffectiveCardMaxCooldown(card);

            using (Font nameFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Font statFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (Font descFont = new Font("Segoe UI", 7.4f, FontStyle.Regular))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (SolidBrush textBrush = new SolidBrush(colorTextSecondary))
            using (StringFormat centerSf = CreateCenterFormat())
            {
                g.DrawString(def.Name, nameFont, whiteBrush, headerRect, centerSf);

                Rectangle statsRect = new Rectangle(rect.X + 6, rect.Y + 34, rect.Width - 12, 20);
                string statText = $"DMG {effectiveDamage}  |  CD {card.CurrentCooldown}/{effectiveMaxCd}";
                g.DrawString(statText, statFont, card.IsReady ? goldBrush : cyanBrush, statsRect, centerSf);

                Rectangle descRect = new Rectangle(rect.X + 6, rect.Y + 56, rect.Width - 12, rect.Height - 78);
                g.DrawString(def.Description, descFont, textBrush, descRect);

                Rectangle catRect = new Rectangle(rect.X + 6, rect.Bottom - 20, rect.Width - 12, 16);
                string footerText = card.IsReady ? $"{def.Category.ToString().ToUpperInvariant()} — READY" : $"COOLING DOWN ({card.CurrentCooldown})";
                g.DrawString(footerText, descFont, card.IsReady ? whiteBrush : cyanBrush, catRect, centerSf);
            }
        }

        private List<string> BuildUpgradeSummaryLines(GameEngine engine)
        {
            List<string> items = new List<string>();
            if (engine.GetMaxHpBonus() > 0)
            {
                items.Add($"+{engine.GetMaxHpBonus()} HP");
            }

            if (engine.GetMovementCooldownReduction() > 0)
            {
                items.Add($"-{engine.GetMovementCooldownReduction()} Move CD");
            }

            if (engine.GetFreeRerollBonus() > 0)
            {
                items.Add($"{engine.GetFreeRerollBonus()} Free Reroll");
            }

            if (engine.GetMeleeDamageBonus() > 0)
            {
                items.Add($"+{engine.GetMeleeDamageBonus()} Melee DMG");
            }

            if (engine.GetWaitEquippedCooldownBonus() > 0)
            {
                items.Add("Wait -1 Eq CD");
            }

            if (engine.GetRangedDamageBonus() > 0)
            {
                items.Add($"+{engine.GetRangedDamageBonus()} Ranged DMG");
            }

            if (engine.GetSpecialDamageBonus() > 0)
            {
                items.Add($"+{engine.GetSpecialDamageBonus()} Special DMG");
            }

            return items;
        }

        private void DrawLegendRow(Graphics g, int x, int y, Color swatchColor, string text, Font font, Brush textBrush)
        {
            Rectangle swatch = new Rectangle(x, y + 2, 14, 14);
            DrawRoundedBox(g, swatch, 3, Color.FromArgb(140, swatchColor.R, swatchColor.G, swatchColor.B), swatchColor, 1.2f);
            g.DrawString(text, font, textBrush, x + 22, y);
        }

        private void DrawCoinIcon(Graphics g, int cx, int cy, int radius)
        {
            Rectangle coinRect = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
            using (SolidBrush fillBrush = new SolidBrush(colorGold))
            using (Pen borderPen = new Pen(Color.FromArgb(255, 224, 128), 1.3f))
            {
                g.FillEllipse(fillBrush, coinRect);
                g.DrawEllipse(borderPen, coinRect);
            }
        }

        private Color GetCategoryColor(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Melee:
                    return Color.FromArgb(186, 58, 58);
                case CardCategory.Ranged:
                    return Color.FromArgb(54, 146, 86);
                case CardCategory.Movement:
                    return Color.FromArgb(52, 118, 186);
                case CardCategory.Special:
                    return colorPurple;
                default:
                    return Color.FromArgb(90, 100, 120);
            }
        }

        private static void DrawGradientButton(
            Graphics g,
            Rectangle bounds,
            Color topColor,
            Color bottomColor,
            Color borderColor,
            string label,
            float fontSize,
            Color textColor)
        {
            using (GraphicsPath path = CreateRoundedRectPath(bounds, 8))
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(bounds, topColor, bottomColor, LinearGradientMode.Vertical))
            using (Pen borderPen = new Pen(borderColor, 1.5f))
            using (Font btnFont = new Font("Segoe UI", fontSize, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(textColor))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
                g.DrawString(label, btnFont, textBrush, bounds, sf);
            }
        }

        private static void DrawRoundedBox(
            Graphics g,
            Rectangle bounds,
            int radius,
            Color fillColor,
            Color borderColor,
            float borderWidth)
        {
            using (GraphicsPath path = CreateRoundedRectPath(bounds, radius))
            {
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

        private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        private static GraphicsPath CreateTopRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddLine(bounds.Right, bounds.Bottom, bounds.X, bounds.Bottom);
            path.CloseFigure();

            return path;
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