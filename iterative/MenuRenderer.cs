#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace RogueCards
{
    public enum MenuActionType
    {
        None,
        SelectCharacter,
        StartNewRun,
        ContinueRun,
        OpenStats,
        BuyWeapon,
        BuyUpgrade,
        Area2Hook
    }

    public class MenuHitbox
    {
        public Rectangle Bounds { get; set; }
        public MenuActionType ActionType { get; set; }
        public string TargetId { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class MenuRenderer
    {
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;

        private readonly List<MenuHitbox> hitboxes = new List<MenuHitbox>();

        private readonly Color colorBgTop = Color.FromArgb(15, 19, 26);
        private readonly Color colorBgBottom = Color.FromArgb(24, 30, 41);
        private readonly Color colorHeader = Color.FromArgb(20, 25, 34);
        private readonly Color colorPanel = Color.FromArgb(27, 33, 44);
        private readonly Color colorCardBg = Color.FromArgb(34, 42, 56);
        private readonly Color colorBorder = Color.FromArgb(58, 70, 90);
        private readonly Color colorGold = Color.FromArgb(228, 174, 62);
        private readonly Color colorGoldDark = Color.FromArgb(166, 118, 28);
        private readonly Color colorCyan = Color.FromArgb(72, 189, 214);
        private readonly Color colorGreen = Color.FromArgb(62, 166, 107);
        private readonly Color colorTextPrimary = Color.FromArgb(238, 242, 248);
        private readonly Color colorTextSecondary = Color.FromArgb(157, 170, 192);
        private readonly Color colorMuted = Color.FromArgb(108, 120, 140);

        public MenuHitbox HitTest(Point point)
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
            GameDatabase database,
            PlayerProfile profile,
            bool hasActiveSave,
            Point mouseLocation,
            string statusMessage)
        {
            hitboxes.Clear();

            DrawBackground(g);
            DrawTopBar(g, profile, mouseLocation);
            DrawHeroColumn(g, database, profile, hasActiveSave, mouseLocation);
            DrawArmoryPanel(g, database, profile, mouseLocation);
            DrawUpgradesPanel(g, database, profile, mouseLocation);
            DrawFooterBar(g, database, statusMessage);
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

        private void DrawTopBar(Graphics g, PlayerProfile profile, Point mouseLocation)
        {
            Rectangle topRect = new Rectangle(0, 0, WindowWidth, 68);
            using (SolidBrush barBrush = new SolidBrush(colorHeader))
            {
                g.FillRectangle(barBrush, topRect);
            }

            using (Pen sepPen = new Pen(colorBorder, 1.5f))
            {
                g.DrawLine(sepPen, 0, 68, WindowWidth, 68);
            }

            using (Font titleFont = new Font("Segoe UI", 20f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            {
                g.DrawString("ROGUE CARDS", titleFont, goldBrush, 28, 9);
                g.DrawString("5x5 TACTICAL ROGUELIKE DECKBUILDER", subFont, mutedBrush, 30, 44);
            }

            Rectangle level1Rect = new Rectangle(350, 17, 200, 34);
            EntityGraphics.DrawRoundedBox(g, level1Rect, 6, Color.FromArgb(40, 52, 68), colorGold, 1.5f);
            using (Font pillFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString("Level 1: Frozen Outpost", pillFont, textBrush, level1Rect, sf);
            }

            Rectangle area2Rect = new Rectangle(562, 17, 178, 34);
            bool hoverArea2 = area2Rect.Contains(mouseLocation);
            Color area2Bg = profile.Area2Unlocked
                ? (hoverArea2 ? Color.FromArgb(46, 68, 86) : Color.FromArgb(35, 52, 68))
                : (hoverArea2 ? Color.FromArgb(36, 41, 52) : Color.FromArgb(28, 33, 42));
            Color area2Border = profile.Area2Unlocked ? colorCyan : colorBorder;

            EntityGraphics.DrawRoundedBox(g, area2Rect, 6, area2Bg, area2Border, 1.5f);
            using (Font pillFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(profile.Area2Unlocked ? colorCyan : colorMuted))
            using (StringFormat sf = CreateCenterFormat())
            {
                string area2Label = profile.Area2Unlocked ? "Area 2: Unlocked" : "Area 2: Locked";
                g.DrawString(area2Label, pillFont, textBrush, area2Rect, sf);
            }

            hitboxes.Add(new MenuHitbox
            {
                Bounds = area2Rect,
                ActionType = MenuActionType.Area2Hook,
                TargetId = "area2",
                IsEnabled = true
            });

            Rectangle coinsRect = new Rectangle(776, 15, 170, 38);
            EntityGraphics.DrawRoundedBox(g, coinsRect, 8, Color.FromArgb(38, 34, 24), colorGold, 1.5f);
            EntityGraphics.DrawCoinIcon(g, coinsRect.X + 20, coinsRect.Y + 19, 10);

            using (Font coinFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                Rectangle textArea = new Rectangle(coinsRect.X + 38, coinsRect.Y, coinsRect.Width - 44, coinsRect.Height);
                g.DrawString($"{profile.Coins} Coins", coinFont, goldBrush, textArea, sf);
            }

            Rectangle statsRect = new Rectangle(964, 15, 288, 38);
            bool hoverStats = statsRect.Contains(mouseLocation);
            Color statsBg = hoverStats ? Color.FromArgb(52, 65, 86) : Color.FromArgb(37, 46, 61);
            EntityGraphics.DrawRoundedBox(g, statsRect, 8, statsBg, hoverStats ? colorGold : colorBorder, 1.5f);
            EntityGraphics.DrawTrophyIcon(g, statsRect.X + 24, statsRect.Y + 19, 10, true);

            using (Font btnFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat sf = CreateCenterFormat())
            {
                Rectangle labelRect = new Rectangle(statsRect.X + 34, statsRect.Y, statsRect.Width - 42, statsRect.Height);
                g.DrawString("Achievements & Statistics", btnFont, textBrush, labelRect, sf);
            }

            hitboxes.Add(new MenuHitbox
            {
                Bounds = statsRect,
                ActionType = MenuActionType.OpenStats,
                TargetId = "stats",
                IsEnabled = true
            });
        }

        private void DrawHeroColumn(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            bool hasActiveSave,
            Point mouseLocation)
        {
            Rectangle colRect = new Rectangle(28, 82, 428, 592);
            EntityGraphics.DrawRoundedBox(g, colRect, 10, colorPanel, colorBorder, 1.5f);

            using (Font sectionFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            {
                g.DrawString("SELECT CHARACTER", sectionFont, goldBrush, colRect.X + 16, colRect.Y + 14);
            }

            int durabilityBonus = CalculateMaxHpBonus(database, profile);
            int cardY = colRect.Y + 46;

            for (int i = 0; i < database.CharacterList.Count && i < 2; i++)
            {
                CharacterDefinition character = database.CharacterList[i];
                Rectangle cardRect = new Rectangle(colRect.X + 14, cardY, colRect.Width - 28, 192);
                bool isUnlocked = profile.UnlockedCharacterIds.Contains(character.Id);
                bool isSelected = string.Equals(profile.SelectedCharacterId, character.Id, StringComparison.OrdinalIgnoreCase);
                bool isHovered = cardRect.Contains(mouseLocation);

                DrawCharacterCard(g, character, cardRect, durabilityBonus, isUnlocked, isSelected, isHovered);

                hitboxes.Add(new MenuHitbox
                {
                    Bounds = cardRect,
                    ActionType = MenuActionType.SelectCharacter,
                    TargetId = character.Id,
                    IsEnabled = isUnlocked
                });

                cardY += 204;
            }

            Rectangle startBtn = new Rectangle(colRect.X + 14, colRect.Bottom - 122, colRect.Width - 28, 54);
            bool hoverStart = startBtn.Contains(mouseLocation);
            Color startTop = hoverStart ? Color.FromArgb(238, 184, 72) : Color.FromArgb(207, 151, 44);
            Color startBottom = hoverStart ? Color.FromArgb(196, 141, 35) : Color.FromArgb(164, 113, 24);
            DrawGradientButton(g, startBtn, startTop, startBottom, colorGold, "START NEW RUN", 13f, Color.FromArgb(18, 20, 26));

            hitboxes.Add(new MenuHitbox
            {
                Bounds = startBtn,
                ActionType = MenuActionType.StartNewRun,
                TargetId = "start",
                IsEnabled = true
            });

            Rectangle continueBtn = new Rectangle(colRect.X + 14, colRect.Bottom - 58, colRect.Width - 28, 44);
            bool hoverContinue = continueBtn.Contains(mouseLocation) && hasActiveSave;
            Color contBg = hasActiveSave
                ? (hoverContinue ? Color.FromArgb(52, 82, 108) : Color.FromArgb(39, 63, 84))
                : Color.FromArgb(30, 35, 45);
            Color contBorder = hasActiveSave ? colorCyan : Color.FromArgb(48, 55, 68);
            Color contText = hasActiveSave ? colorTextPrimary : colorMuted;
            string contLabel = hasActiveSave ? "CONTINUE SAVED RUN" : "CONTINUE SAVED RUN (NO SAVE)";

            EntityGraphics.DrawRoundedBox(g, continueBtn, 8, contBg, contBorder, 1.5f);
            using (Font btnFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(contText))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString(contLabel, btnFont, textBrush, continueBtn, sf);
            }

            hitboxes.Add(new MenuHitbox
            {
                Bounds = continueBtn,
                ActionType = MenuActionType.ContinueRun,
                TargetId = "continue",
                IsEnabled = hasActiveSave
            });
        }

        private void DrawCharacterCard(
            Graphics g,
            CharacterDefinition character,
            Rectangle rect,
            int durabilityBonus,
            bool isUnlocked,
            bool isSelected,
            bool isHovered)
        {
            Color topBg = isSelected
                ? Color.FromArgb(44, 58, 80)
                : (isHovered && isUnlocked ? Color.FromArgb(42, 52, 68) : Color.FromArgb(37, 46, 61));
            Color botBg = isSelected
                ? Color.FromArgb(30, 40, 56)
                : Color.FromArgb(26, 33, 45);

            if (!isUnlocked)
            {
                topBg = Color.FromArgb(30, 34, 44);
                botBg = Color.FromArgb(24, 28, 36);
            }

            Color border = isSelected
                ? colorGold
                : (isHovered && isUnlocked ? colorCyan : colorBorder);

            EntityGraphics.DrawGradientRoundedBox(g, rect, 10, topBg, botBg, border, isSelected ? 2.5f : 1.5f);

            Rectangle portraitRect = new Rectangle(rect.X + 12, rect.Y + 14, 96, 114);
            Color portraitTop = character.Id == "viking"
                ? Color.FromArgb(30, 48, 74)
                : Color.FromArgb(24, 54, 68);
            Color portraitBot = Color.FromArgb(16, 22, 32);

            EntityGraphics.DrawGradientRoundedBox(g, portraitRect, 8, portraitTop, portraitBot, isSelected ? colorGold : colorBorder, 1.5f);

            int pcx = portraitRect.X + (portraitRect.Width / 2);
            int pcy = portraitRect.Y + (portraitRect.Height / 2) + 4;

            if (character.Id == "viking")
            {
                EntityGraphics.DrawViking(g, pcx, pcy, 1.48f);
            }
            else
            {
                EntityGraphics.DrawShieldmaiden(g, pcx, pcy, 1.48f, !isUnlocked);
                if (!isUnlocked)
                {
                    using (SolidBrush veil = new SolidBrush(Color.FromArgb(88, 10, 14, 20)))
                    {
                        g.FillRectangle(veil, portraitRect);
                    }

                    EntityGraphics.DrawLockIcon(g, pcx, pcy);
                }
            }

            int infoX = portraitRect.Right + 14;
            int infoWidth = rect.Right - infoX - 12;

            using (Font nameFont = new Font("Segoe UI", 13f, FontStyle.Bold))
            using (Font badgeFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (Font statFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font descFont = new Font("Segoe UI", 8.8f, FontStyle.Regular))
            using (SolidBrush primaryBrush = new SolidBrush(isUnlocked ? colorTextPrimary : colorMuted))
            using (SolidBrush secondaryBrush = new SolidBrush(isUnlocked ? colorTextSecondary : colorMuted))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            {
                g.DrawString(character.Name, nameFont, primaryBrush, infoX, rect.Y + 12);

                Rectangle statusBadge = new Rectangle(rect.Right - 96, rect.Y + 14, 84, 22);
                string badgeText = isSelected ? "SELECTED" : (isUnlocked ? "READY" : "LOCKED");
                Color badgeBg = isSelected
                    ? Color.FromArgb(148, 106, 28)
                    : (isUnlocked ? Color.FromArgb(40, 88, 66) : Color.FromArgb(95, 42, 46));
                EntityGraphics.DrawRoundedBox(g, statusBadge, 5, badgeBg, badgeBg, 1f);

                using (StringFormat sf = CreateCenterFormat())
                using (SolidBrush whiteBrush = new SolidBrush(Color.White))
                {
                    g.DrawString(badgeText, badgeFont, whiteBrush, statusBadge, sf);
                }

                int totalHp = character.BaseHp + durabilityBonus;
                string hpLabel = durabilityBonus > 0
                    ? $"HP: {totalHp} ({character.BaseHp}+{durabilityBonus})   |   CD: {character.SpecialCooldown} Turns"
                    : $"HP: {totalHp}   |   Special CD: {character.SpecialCooldown} Turns";
                g.DrawString(hpLabel, statFont, isUnlocked ? cyanBrush : secondaryBrush, infoX, rect.Y + 42);

                g.DrawString($"Special: {character.SpecialName}", statFont, isUnlocked ? goldBrush : secondaryBrush, infoX, rect.Y + 66);

                Rectangle descRect = new Rectangle(infoX, rect.Y + 88, infoWidth, 64);
                g.DrawString(character.SpecialDescription, descFont, secondaryBrush, descRect);

                Rectangle bottomBanner = new Rectangle(rect.X + 12, rect.Bottom - 36, rect.Width - 24, 24);
                if (isUnlocked)
                {
                    string hint = isSelected ? "Active hero for next run" : "Click to select this hero";
                    g.DrawString(hint, badgeFont, isSelected ? goldBrush : secondaryBrush, bottomBanner.X + 2, bottomBanner.Y + 4);
                }
                else
                {
                    EntityGraphics.DrawRoundedBox(g, bottomBanner, 5, Color.FromArgb(52, 30, 34), Color.FromArgb(145, 62, 68), 1f);
                    using (StringFormat sf = CreateCenterFormat())
                    using (SolidBrush lockBrush = new SolidBrush(Color.FromArgb(240, 175, 175)))
                    {
                        g.DrawString("Unlock: Complete 'Kingslayer' Achievement (Defeat Boss)", badgeFont, lockBrush, bottomBanner, sf);
                    }
                }
            }
        }

        private void DrawArmoryPanel(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            Point mouseLocation)
        {
            Rectangle panelRect = new Rectangle(474, 82, 778, 316);
            EntityGraphics.DrawRoundedBox(g, panelRect, 10, colorPanel, colorBorder, 1.5f);

            int totalWeapons = database.WeaponList.Count;
            int unlockedCount = database.WeaponList.Count(w => profile.UnlockedWeaponIds.Contains(w.Id));

            using (Font titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (Font countFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            using (StringFormat rightFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
            {
                g.DrawString("ARMORY — NEXT UNLOCKABLE WEAPONS", titleFont, goldBrush, panelRect.X + 16, panelRect.Y + 13);
                g.DrawString(
                    $"Unlocked: {unlockedCount} / {totalWeapons}",
                    countFont,
                    mutedBrush,
                    new Rectangle(panelRect.Right - 220, panelRect.Y + 15, 204, 24),
                    rightFormat);
            }

            List<WeaponDefinition> nextWeapons = database.WeaponList
                .Where(w => !profile.UnlockedWeaponIds.Contains(w.Id))
                .OrderBy(w => w.UnlockOrder)
                .Take(2)
                .ToList();

            if (nextWeapons.Count == 0)
            {
                Rectangle doneRect = new Rectangle(panelRect.X + 20, panelRect.Y + 52, panelRect.Width - 40, panelRect.Height - 72);
                EntityGraphics.DrawRoundedBox(g, doneRect, 10, colorCardBg, colorGreen, 1.5f);
                using (Font doneFont = new Font("Segoe UI", 14f, FontStyle.Bold))
                using (SolidBrush greenBrush = new SolidBrush(colorGreen))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString("ALL WEAPONS & CARDS UNLOCKED!", doneFont, greenBrush, doneRect, sf);
                }
                return;
            }

            int cardWidth = 362;
            int cardHeight = 252;
            int startX = panelRect.X + 16;
            int cardY = panelRect.Y + 48;

            for (int i = 0; i < nextWeapons.Count; i++)
            {
                WeaponDefinition weapon = nextWeapons[i];
                Rectangle cardRect = new Rectangle(startX + (i * (cardWidth + 22)), cardY, cardWidth, cardHeight);
                DrawShopWeaponCard(g, weapon, cardRect, profile.Coins, mouseLocation);
            }
        }

        private void DrawShopWeaponCard(
            Graphics g,
            WeaponDefinition weapon,
            Rectangle rect,
            int playerCoins,
            Point mouseLocation)
        {
            Color catColor = GetCategoryColor(weapon.Category);
            Color catDark = Color.FromArgb(catColor.A, (int)(catColor.R * 0.72f), (int)(catColor.G * 0.72f), (int)(catColor.B * 0.72f));
            bool canAfford = playerCoins >= weapon.UnlockCost;
            Rectangle buyBtn = new Rectangle(rect.X + 14, rect.Bottom - 54, rect.Width - 28, 40);
            bool hoverBuy = buyBtn.Contains(mouseLocation);
            bool hoverCard = rect.Contains(mouseLocation);

            Rectangle drawRect = hoverCard && canAfford
                ? new Rectangle(rect.X, rect.Y - 3, rect.Width, rect.Height)
                : rect;

            Rectangle shadowRect = new Rectangle(drawRect.X + 4, drawRect.Y + 5, drawRect.Width, drawRect.Height);
            EntityGraphics.DrawRoundedBox(g, shadowRect, 10, Color.FromArgb(115, 0, 0, 0), Color.Transparent, 0f);

            EntityGraphics.DrawGradientRoundedBox(
                g,
                drawRect,
                10,
                Color.FromArgb(39, 48, 64),
                Color.FromArgb(27, 34, 46),
                hoverCard ? catColor : colorBorder,
                hoverCard ? 2.2f : 1.5f);

            Rectangle headerRect = new Rectangle(drawRect.X + 2, drawRect.Y + 2, drawRect.Width - 4, 42);
            using (GraphicsPath headerPath = EntityGraphics.CreateTopRoundedRectPath(headerRect, 8))
            using (LinearGradientBrush headerBrush = new LinearGradientBrush(headerRect, catColor, catDark, LinearGradientMode.Vertical))
            {
                g.FillPath(headerBrush, headerPath);
            }

            EntityGraphics.DrawCategoryIcon(g, weapon.Category, drawRect.X + 20, drawRect.Y + 23, 14);

            using (Font nameFont = new Font("Segoe UI", 11.8f, FontStyle.Bold))
            using (Font badgeFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Font descFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            using (SolidBrush secondaryBrush = new SolidBrush(colorTextPrimary))
            {
                g.DrawString(weapon.Name, nameFont, whiteBrush, drawRect.X + 34, drawRect.Y + 11);

                Rectangle catPill = new Rectangle(drawRect.Right - 96, drawRect.Y + 11, 84, 24);
                EntityGraphics.DrawRoundedBox(g, catPill, 5, Color.FromArgb(90, 0, 0, 0), Color.FromArgb(140, 255, 255, 255), 1f);
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString(weapon.Category.ToString().ToUpperInvariant(), badgeFont, whiteBrush, catPill, sf);
                }

                Rectangle dmgPill = new Rectangle(drawRect.X + 14, drawRect.Y + 54, 102, 28);
                Rectangle cdPill = new Rectangle(drawRect.X + 124, drawRect.Y + 54, 110, 28);
                Rectangle aimPill = new Rectangle(drawRect.X + 242, drawRect.Y + 54, 106, 28);

                DrawStatPill(g, dmgPill, $"DMG: {weapon.BaseDamage}", Color.FromArgb(228, 104, 96));
                DrawStatPill(g, cdPill, $"CD: {weapon.BaseCooldown} Turns", colorCyan);
                DrawStatPill(g, aimPill, weapon.Targeting.ToString(), colorGold);

                Rectangle descBox = new Rectangle(drawRect.X + 14, drawRect.Y + 94, drawRect.Width - 28, 90);
                EntityGraphics.DrawRoundedBox(g, descBox, 6, Color.FromArgb(24, 30, 41), Color.FromArgb(46, 56, 73), 1f);
                Rectangle descInner = new Rectangle(descBox.X + 10, descBox.Y + 8, descBox.Width - 20, descBox.Height - 16);
                g.DrawString(weapon.Description, descFont, secondaryBrush, descInner);
            }

            Rectangle drawBuyBtn = new Rectangle(drawRect.X + 14, drawRect.Bottom - 54, drawRect.Width - 28, 40);
            if (canAfford)
            {
                Color btnTop = hoverBuy ? Color.FromArgb(84, 196, 130) : Color.FromArgb(62, 166, 107);
                Color btnBottom = hoverBuy ? Color.FromArgb(58, 156, 98) : Color.FromArgb(44, 128, 80);
                DrawGradientButton(
                    g,
                    drawBuyBtn,
                    btnTop,
                    btnBottom,
                    Color.FromArgb(118, 224, 158),
                    $"UNLOCK WEAPON ({weapon.UnlockCost} COINS)",
                    10f,
                    Color.White);
            }
            else
            {
                EntityGraphics.DrawRoundedBox(g, drawBuyBtn, 7, Color.FromArgb(35, 40, 51), Color.FromArgb(62, 70, 86), 1.5f);
                using (Font btnFont = new Font("Segoe UI", 9.8f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString($"NEED {weapon.UnlockCost} COINS", btnFont, mutedBrush, drawBuyBtn, sf);
                }
            }

            hitboxes.Add(new MenuHitbox
            {
                Bounds = buyBtn,
                ActionType = MenuActionType.BuyWeapon,
                TargetId = weapon.Id,
                IsEnabled = true
            });
        }

        private void DrawUpgradesPanel(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            Point mouseLocation)
        {
            Rectangle panelRect = new Rectangle(474, 412, 778, 262);
            EntityGraphics.DrawRoundedBox(g, panelRect, 10, colorPanel, colorBorder, 1.5f);

            int totalUpgrades = database.UpgradeList.Count;
            int unlockedCount = database.UpgradeList.Count(u => profile.UnlockedUpgradeIds.Contains(u.Id));

            using (Font titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (Font countFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            using (StringFormat rightFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
            {
                g.DrawString("PERMANENT UPGRADES — TRAINING QUEUE", titleFont, goldBrush, panelRect.X + 16, panelRect.Y + 13);
                g.DrawString(
                    $"Unlocked: {unlockedCount} / {totalUpgrades}",
                    countFont,
                    mutedBrush,
                    new Rectangle(panelRect.Right - 220, panelRect.Y + 15, 204, 24),
                    rightFormat);
            }

            List<PermanentUpgradeDefinition> nextUpgrades = database.UpgradeList
                .Where(u => !profile.UnlockedUpgradeIds.Contains(u.Id))
                .OrderBy(u => u.UnlockOrder)
                .Take(2)
                .ToList();

            if (nextUpgrades.Count == 0)
            {
                Rectangle doneRect = new Rectangle(panelRect.X + 20, panelRect.Y + 52, panelRect.Width - 40, panelRect.Height - 72);
                EntityGraphics.DrawRoundedBox(g, doneRect, 10, colorCardBg, colorGold, 1.5f);
                using (Font doneFont = new Font("Segoe UI", 14f, FontStyle.Bold))
                using (SolidBrush goldBrush = new SolidBrush(colorGold))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString("ALL PERMANENT UPGRADES MASTERED!", doneFont, goldBrush, doneRect, sf);
                }
                return;
            }

            int cardWidth = 362;
            int cardHeight = 198;
            int startX = panelRect.X + 16;
            int cardY = panelRect.Y + 48;

            for (int i = 0; i < nextUpgrades.Count; i++)
            {
                PermanentUpgradeDefinition upgrade = nextUpgrades[i];
                Rectangle cardRect = new Rectangle(startX + (i * (cardWidth + 22)), cardY, cardWidth, cardHeight);
                DrawShopUpgradeCard(g, upgrade, cardRect, profile.Coins, mouseLocation);
            }
        }

        private void DrawShopUpgradeCard(
            Graphics g,
            PermanentUpgradeDefinition upgrade,
            Rectangle rect,
            int playerCoins,
            Point mouseLocation)
        {
            bool canAfford = playerCoins >= upgrade.Cost;
            Rectangle buyBtn = new Rectangle(rect.X + 14, rect.Bottom - 52, rect.Width - 28, 38);
            bool hoverBuy = buyBtn.Contains(mouseLocation);
            bool hoverCard = rect.Contains(mouseLocation);

            EntityGraphics.DrawGradientRoundedBox(
                g,
                rect,
                10,
                Color.FromArgb(39, 48, 64),
                Color.FromArgb(27, 34, 46),
                hoverCard ? colorGold : colorBorder,
                hoverCard ? 2f : 1.5f);

            Rectangle headerRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, 40);
            using (GraphicsPath headerPath = EntityGraphics.CreateTopRoundedRectPath(headerRect, 8))
            using (SolidBrush headerBrush = new SolidBrush(Color.FromArgb(46, 56, 74)))
            {
                g.FillPath(headerBrush, headerPath);
            }

            using (Font nameFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (Font tierFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Font descFont = new Font("Segoe UI", 9.8f, FontStyle.Regular))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush textBrush = new SolidBrush(colorTextPrimary))
            {
                g.DrawString(upgrade.Name, nameFont, goldBrush, rect.X + 14, rect.Y + 10);

                Rectangle tierPill = new Rectangle(rect.Right - 76, rect.Y + 10, 64, 22);
                EntityGraphics.DrawRoundedBox(g, tierPill, 5, Color.FromArgb(30, 37, 50), colorGoldDark, 1f);
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString($"TIER #{upgrade.UnlockOrder}", tierFont, goldBrush, tierPill, sf);
                }

                Rectangle descBox = new Rectangle(rect.X + 14, rect.Y + 52, rect.Width - 28, 82);
                EntityGraphics.DrawRoundedBox(g, descBox, 6, Color.FromArgb(24, 30, 41), Color.FromArgb(46, 56, 73), 1f);
                Rectangle descInner = new Rectangle(descBox.X + 10, descBox.Y + 10, descBox.Width - 20, descBox.Height - 20);
                g.DrawString(upgrade.Description, descFont, textBrush, descInner);
            }

            if (canAfford)
            {
                Color btnTop = hoverBuy ? Color.FromArgb(236, 184, 72) : Color.FromArgb(204, 150, 44);
                Color btnBottom = hoverBuy ? Color.FromArgb(196, 140, 34) : Color.FromArgb(164, 114, 24);
                DrawGradientButton(
                    g,
                    buyBtn,
                    btnTop,
                    btnBottom,
                    colorGold,
                    $"PURCHASE UPGRADE ({upgrade.Cost} COINS)",
                    9.8f,
                    Color.FromArgb(18, 20, 26));
            }
            else
            {
                EntityGraphics.DrawRoundedBox(g, buyBtn, 7, Color.FromArgb(35, 40, 51), Color.FromArgb(62, 70, 86), 1.5f);
                using (Font btnFont = new Font("Segoe UI", 9.8f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString($"NEED {upgrade.Cost} COINS", btnFont, mutedBrush, buyBtn, sf);
                }
            }

            hitboxes.Add(new MenuHitbox
            {
                Bounds = buyBtn,
                ActionType = MenuActionType.BuyUpgrade,
                TargetId = upgrade.Id,
                IsEnabled = true
            });
        }

        private void DrawFooterBar(Graphics g, GameDatabase database, string statusMessage)
        {
            Rectangle footerRect = new Rectangle(0, WindowHeight - 36, WindowWidth, 36);
            using (SolidBrush barBrush = new SolidBrush(colorHeader))
            {
                g.FillRectangle(barBrush, footerRect);
            }

            using (Pen sepPen = new Pen(colorBorder, 1f))
            {
                g.DrawLine(sepPen, 0, footerRect.Y, WindowWidth, footerRect.Y);
            }

            using (Font footerFont = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(colorTextSecondary))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (StringFormat leftFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (StringFormat rightFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                Rectangle leftRect = new Rectangle(28, footerRect.Y, 760, footerRect.Height);
                string message = string.IsNullOrWhiteSpace(statusMessage)
                    ? $"Loaded Data: {database.WeaponList.Count} Weapons, {database.Enemies.Count} Enemies, {database.Waves.Count} Waves, {database.UpgradeList.Count} Upgrades, {database.AchievementList.Count} Achievements."
                    : statusMessage;

                g.DrawString(message, footerFont, string.IsNullOrWhiteSpace(statusMessage) ? textBrush : goldBrush, leftRect, leftFormat);

                Rectangle rightRect = new Rectangle(800, footerRect.Y, WindowWidth - 828, footerRect.Height);
                g.DrawString("Single-Screen Tactical UI | Pure Procedural GDI+ Vector Art", footerFont, textBrush, rightRect, rightFormat);
            }
        }

        private void DrawStatPill(Graphics g, Rectangle rect, string text, Color textColor)
        {
            EntityGraphics.DrawRoundedBox(g, rect, 5, Color.FromArgb(24, 30, 41), Color.FromArgb(52, 64, 82), 1f);
            using (Font pillFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(textColor))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString(text, pillFont, brush, rect, sf);
            }
        }

        private static Color GetCategoryColor(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Melee:
                    return Color.FromArgb(192, 58, 58);
                case CardCategory.Ranged:
                    return Color.FromArgb(52, 152, 88);
                case CardCategory.Movement:
                    return Color.FromArgb(52, 122, 194);
                case CardCategory.Special:
                    return Color.FromArgb(142, 78, 190);
                default:
                    return Color.FromArgb(90, 100, 120);
            }
        }

        private static int CalculateMaxHpBonus(GameDatabase database, PlayerProfile profile)
        {
            int bonus = 0;
            foreach (PermanentUpgradeDefinition upgrade in database.UpgradeList)
            {
                if (upgrade.EffectType == PermanentUpgradeEffectType.MaxHp &&
                    profile.UnlockedUpgradeIds.Contains(upgrade.Id))
                {
                    bonus += upgrade.EffectValue;
                }
            }

            return bonus;
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
            EntityGraphics.DrawGradientRoundedBox(g, bounds, 8, topColor, bottomColor, borderColor, 1.5f);
            using (Font btnFont = new Font("Segoe UI", fontSize, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(textColor))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString(label, btnFont, textBrush, bounds, sf);
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