#nullable disable

namespace RogueCards
{
    public enum MenuActionType
    {
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
        public Rectangle Bounds { get; init; }
        public MenuActionType ActionType { get; init; }
        public string TargetId { get; init; }
        public bool IsEnabled { get; init; }
    }

    public class MenuRenderer
    {
        private readonly List<MenuHitbox> _hitboxes = [];

        public MenuHitbox HitTest(Point point) => UiHelpers.HitTest(_hitboxes, point, static h => h.Bounds);

        public void Render(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            bool hasActiveSave,
            Point mouseLocation,
            string statusMessage)
        {
            _hitboxes.Clear();

            UiHelpers.DrawBackground(g, UiTheme.MenuBgBottom);
            DrawTopBar(g, profile, mouseLocation);
            DrawHeroColumn(g, database, profile, hasActiveSave, mouseLocation);
            DrawArmoryPanel(g, database, profile, mouseLocation);
            DrawUpgradesPanel(g, database, profile, mouseLocation);
            DrawFooterBar(g, database, statusMessage);
        }

        private void DrawTopBar(Graphics g, PlayerProfile profile, Point mouseLocation)
        {
            UiHelpers.DrawHeaderBar(g, 68);
            DrawTitleAndLevelPills(g, profile, mouseLocation);
            DrawCoinsAndStatsButtons(g, profile, mouseLocation);
        }

        private void DrawTitleAndLevelPills(Graphics g, PlayerProfile profile, Point mouseLocation)
        {
            using (Font titleFont = new("Segoe UI", 20f, FontStyle.Bold))
            using (Font subFont = new("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            using (SolidBrush mutedBrush = new(UiTheme.TextSecondary))
            {
                g.DrawString("ROGUE CARDS", titleFont, goldBrush, 28, 9);
                g.DrawString("5x5 TACTICAL ROGUELIKE DECKBUILDER", subFont, mutedBrush, 30, 44);
            }

            Rectangle level1Rect = new(350, 17, 200, 34);
            UiHelpers.DrawRoundedBox(g, level1Rect, 6, Color.FromArgb(40, 52, 68), UiTheme.Gold, 1.5f);
            using (Font pillFont = new("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new(UiTheme.TextPrimary))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                g.DrawString("Level 1: Frozen Outpost", pillFont, textBrush, level1Rect, sf);
            }

            Rectangle area2Rect = new(562, 17, 178, 34);
            bool hoverArea2 = area2Rect.Contains(mouseLocation);
            Color area2Bg = profile.Area2Unlocked
                ? (hoverArea2 ? Color.FromArgb(46, 68, 86) : Color.FromArgb(35, 52, 68))
                : (hoverArea2 ? Color.FromArgb(36, 41, 52) : Color.FromArgb(28, 33, 42));

            UiHelpers.DrawRoundedBox(g, area2Rect, 6, area2Bg, profile.Area2Unlocked ? UiTheme.Cyan : UiTheme.Border, 1.5f);
            using Font areaFont = new("Segoe UI", 9f, FontStyle.Bold);
            using SolidBrush areaBrush = new(profile.Area2Unlocked ? UiTheme.Cyan : UiTheme.Muted);
            using StringFormat centerSf = UiHelpers.CreateStringFormat();
            g.DrawString(profile.Area2Unlocked ? "Area 2: Unlocked" : "Area 2: Locked", areaFont, areaBrush, area2Rect, centerSf);

            AddHitbox(area2Rect, MenuActionType.Area2Hook, "area2", true);
        }

        private void DrawCoinsAndStatsButtons(Graphics g, PlayerProfile profile, Point mouseLocation)
        {
            Rectangle coinsRect = new(776, 15, 170, 38);
            UiHelpers.DrawRoundedBox(g, coinsRect, 8, Color.FromArgb(38, 34, 24), UiTheme.Gold, 1.5f);
            EntityGraphics.DrawCoinIcon(g, coinsRect.X + 20, coinsRect.Y + 19, 10);

            using (Font coinFont = new("Segoe UI", 12f, FontStyle.Bold))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            using (StringFormat leftSf = UiHelpers.CreateStringFormat(StringAlignment.Near))
            {
                Rectangle textArea = new(coinsRect.X + 38, coinsRect.Y, coinsRect.Width - 44, coinsRect.Height);
                g.DrawString($"{profile.Coins} Coins", coinFont, goldBrush, textArea, leftSf);
            }

            Rectangle statsRect = new(964, 15, 288, 38);
            bool hoverStats = statsRect.Contains(mouseLocation);
            Color statsBg = hoverStats ? Color.FromArgb(52, 65, 86) : Color.FromArgb(37, 46, 61);
            UiHelpers.DrawRoundedBox(g, statsRect, 8, statsBg, hoverStats ? UiTheme.Gold : UiTheme.Border, 1.5f);
            EntityGraphics.DrawTrophyIcon(g, statsRect.X + 24, statsRect.Y + 19, 10, true);

            using Font btnFont = new("Segoe UI", 10.5f, FontStyle.Bold);
            using SolidBrush textBrush = new(UiTheme.TextPrimary);
            using StringFormat centerSf = UiHelpers.CreateStringFormat();
            Rectangle labelRect = new(statsRect.X + 34, statsRect.Y, statsRect.Width - 42, statsRect.Height);
            g.DrawString("Achievements & Statistics", btnFont, textBrush, labelRect, centerSf);

            AddHitbox(statsRect, MenuActionType.OpenStats, "stats", true);
        }

        private void DrawHeroColumn(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            bool hasActiveSave,
            Point mouseLocation)
        {
            Rectangle colRect = new(28, 82, 428, 592);
            UiHelpers.DrawRoundedBox(g, colRect, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using (Font sectionFont = new("Segoe UI", 11.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            {
                g.DrawString("SELECT CHARACTER", sectionFont, goldBrush, colRect.X + 16, colRect.Y + 14);
            }

            int durabilityBonus = CalculateMaxHpBonus(database, profile);
            int cardY = colRect.Y + 46;

            for (int i = 0; i < database.CharacterList.Count && i < 2; i++)
            {
                CharacterDefinition character = database.CharacterList[i];
                Rectangle cardRect = new(colRect.X + 14, cardY, colRect.Width - 28, 192);
                bool isUnlocked = profile.UnlockedCharacterIds.Contains(character.Id);
                bool isSelected = string.Equals(profile.SelectedCharacterId, character.Id, StringComparison.OrdinalIgnoreCase);

                DrawCharacterCard(g, database, character, cardRect, durabilityBonus, isUnlocked, isSelected, cardRect.Contains(mouseLocation));
                AddHitbox(cardRect, MenuActionType.SelectCharacter, character.Id, isUnlocked);
                cardY += 204;
            }

            DrawRunActionButtons(g, colRect, hasActiveSave, mouseLocation);
        }

        private void DrawRunActionButtons(Graphics g, Rectangle colRect, bool hasActiveSave, Point mouseLocation)
        {
            Rectangle startBtn = new(colRect.X + 14, colRect.Bottom - 122, colRect.Width - 28, 54);
            bool hoverStart = startBtn.Contains(mouseLocation);
            UiHelpers.DrawGradientButton(
                g,
                startBtn,
                hoverStart ? Color.FromArgb(238, 184, 72) : Color.FromArgb(207, 151, 44),
                hoverStart ? Color.FromArgb(196, 141, 35) : Color.FromArgb(164, 113, 24),
                UiTheme.Gold,
                "START NEW RUN",
                13f,
                UiTheme.DarkButtonText);
            AddHitbox(startBtn, MenuActionType.StartNewRun, "start", true);

            Rectangle continueBtn = new(colRect.X + 14, colRect.Bottom - 58, colRect.Width - 28, 44);
            bool hoverContinue = continueBtn.Contains(mouseLocation) && hasActiveSave;
            Color contBg = hasActiveSave
                ? (hoverContinue ? Color.FromArgb(52, 82, 108) : Color.FromArgb(39, 63, 84))
                : Color.FromArgb(30, 35, 45);
            Color contBorder = hasActiveSave ? UiTheme.Cyan : Color.FromArgb(48, 55, 68);
            string contLabel = hasActiveSave ? "CONTINUE SAVED RUN" : "CONTINUE SAVED RUN (NO SAVE)";

            UiHelpers.DrawRoundedBox(g, continueBtn, 8, contBg, contBorder, 1.5f);
            using Font btnFont = new("Segoe UI", 10.5f, FontStyle.Bold);
            using SolidBrush textBrush = new(hasActiveSave ? UiTheme.TextPrimary : UiTheme.Muted);
            using StringFormat sf = UiHelpers.CreateStringFormat();
            g.DrawString(contLabel, btnFont, textBrush, continueBtn, sf);
            AddHitbox(continueBtn, MenuActionType.ContinueRun, "continue", hasActiveSave);
        }

        private static void DrawCharacterCard(
            Graphics g,
            GameDatabase database,
            CharacterDefinition character,
            Rectangle rect,
            int durabilityBonus,
            bool isUnlocked,
            bool isSelected,
            bool isHovered)
        {
            Color topBg = !isUnlocked
                ? Color.FromArgb(30, 34, 44)
                : (isSelected ? Color.FromArgb(44, 58, 80) : (isHovered ? Color.FromArgb(42, 52, 68) : Color.FromArgb(37, 46, 61)));
            Color botBg = !isUnlocked
                ? Color.FromArgb(24, 28, 36)
                : (isSelected ? Color.FromArgb(30, 40, 56) : Color.FromArgb(26, 33, 45));
            Color border = isSelected ? UiTheme.Gold : (isHovered && isUnlocked ? UiTheme.Cyan : UiTheme.Border);

            UiHelpers.DrawGradientRoundedBox(g, rect, 10, topBg, botBg, border, isSelected ? 2.5f : 1.5f);

            Rectangle portraitRect = new(rect.X + 12, rect.Y + 14, 96, 114);
            DrawCharacterPortrait(g, character, portraitRect, isUnlocked, isSelected);
            DrawCharacterDetails(g, database, character, rect, portraitRect.Right + 14, durabilityBonus, isUnlocked, isSelected);
        }

        private static void DrawCharacterPortrait(
            Graphics g,
            CharacterDefinition character,
            Rectangle portraitRect,
            bool isUnlocked,
            bool isSelected)
        {
            Color portraitTop = character.Id == "viking" ? Color.FromArgb(30, 48, 74) : Color.FromArgb(24, 54, 68);
            UiHelpers.DrawGradientRoundedBox(g, portraitRect, 8, portraitTop, Color.FromArgb(16, 22, 32), isSelected ? UiTheme.Gold : UiTheme.Border, 1.5f);

            int pcx = portraitRect.X + (portraitRect.Width / 2);
            int pcy = portraitRect.Y + (portraitRect.Height / 2) + 4;

            if (character.Id == "viking")
            {
                EntityGraphics.DrawViking(g, pcx, pcy, 1.48f);
                return;
            }

            EntityGraphics.DrawShieldmaiden(g, pcx, pcy, 1.48f, !isUnlocked);
            if (!isUnlocked)
            {
                using SolidBrush veil = new(Color.FromArgb(88, 10, 14, 20));
                g.FillRectangle(veil, portraitRect);
                EntityGraphics.DrawLockIcon(g, pcx, pcy);
            }
        }

        private static void DrawCharacterDetails(
            Graphics g,
            GameDatabase database,
            CharacterDefinition character,
            Rectangle rect,
            int infoX,
            int durabilityBonus,
            bool isUnlocked,
            bool isSelected)
        {
            int infoWidth = rect.Right - infoX - 12;

            using Font nameFont = new("Segoe UI", 13f, FontStyle.Bold);
            using Font badgeFont = new("Segoe UI", 8f, FontStyle.Bold);
            using Font statFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font descFont = new("Segoe UI", 8.8f, FontStyle.Regular);
            using SolidBrush primaryBrush = new(isUnlocked ? UiTheme.TextPrimary : UiTheme.Muted);
            using SolidBrush secondaryBrush = new(isUnlocked ? UiTheme.TextSecondary : UiTheme.Muted);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush cyanBrush = new(UiTheme.Cyan);
            using StringFormat sf = UiHelpers.CreateStringFormat();

            g.DrawString(character.Name, nameFont, primaryBrush, infoX, rect.Y + 12);

            Rectangle statusBadge = new(rect.Right - 96, rect.Y + 14, 84, 22);
            string badgeText = isSelected ? "SELECTED" : (isUnlocked ? "READY" : "LOCKED");
            Color badgeBg = isSelected
                ? Color.FromArgb(148, 106, 28)
                : (isUnlocked ? Color.FromArgb(40, 88, 66) : Color.FromArgb(95, 42, 46));
            UiHelpers.DrawRoundedBox(g, statusBadge, 5, badgeBg, badgeBg, 1f);
            using (SolidBrush whiteBrush = new(Color.White))
            {
                g.DrawString(badgeText, badgeFont, whiteBrush, statusBadge, sf);
            }

            int totalHp = character.BaseHp + durabilityBonus;
            string hpLabel = durabilityBonus > 0
                ? $"HP: {totalHp} ({character.BaseHp}+{durabilityBonus})   |   CD: {character.SpecialCooldown} Turns"
                : $"HP: {totalHp}   |   Special CD: {character.SpecialCooldown} Turns";
            g.DrawString(hpLabel, statFont, isUnlocked ? cyanBrush : secondaryBrush, infoX, rect.Y + 42);
            g.DrawString($"Special: {character.SpecialName}", statFont, isUnlocked ? goldBrush : secondaryBrush, infoX, rect.Y + 66);
            g.DrawString(character.SpecialDescription, descFont, secondaryBrush, new Rectangle(infoX, rect.Y + 88, infoWidth, 64));

            Rectangle bottomBanner = new(rect.X + 12, rect.Bottom - 36, rect.Width - 24, 24);
            if (isUnlocked)
            {
                string hint = isSelected ? "Active hero for next run" : "Click to select this hero";
                g.DrawString(hint, badgeFont, isSelected ? goldBrush : secondaryBrush, bottomBanner.X + 2, bottomBanner.Y + 4);
                return;
            }

            string achievementName = !string.IsNullOrEmpty(character.UnlockAchievementId) &&
                                     database.Achievements.TryGetValue(character.UnlockAchievementId, out AchievementDefinition ach)
                ? ach.Title
                : "Kingslayer";

            UiHelpers.DrawRoundedBox(g, bottomBanner, 5, Color.FromArgb(52, 30, 34), Color.FromArgb(145, 62, 68), 1f);
            using SolidBrush lockBrush = new(Color.FromArgb(240, 175, 175));
            g.DrawString($"Unlock: Complete '{achievementName}' Achievement (Defeat Boss)", badgeFont, lockBrush, bottomBanner, sf);
        }

        private void DrawArmoryPanel(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            Point mouseLocation)
        {
            Rectangle panelRect = new(474, 82, 778, 316);
            UiHelpers.DrawRoundedBox(g, panelRect, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            int unlockedCount = database.WeaponList.Count(w => profile.UnlockedWeaponIds.Contains(w.Id));
            DrawShopPanelHeader(g, panelRect, "ARMORY — NEXT UNLOCKABLE WEAPONS", unlockedCount, database.WeaponList.Count);

            List<WeaponDefinition> nextWeapons = database.WeaponList
                .Where(w => !profile.UnlockedWeaponIds.Contains(w.Id))
                .OrderBy(w => w.UnlockOrder)
                .Take(2)
                .ToList();

            if (nextWeapons.Count == 0)
            {
                DrawAllUnlockedBanner(g, panelRect, "ALL WEAPONS & CARDS UNLOCKED!", UiTheme.Green);
                return;
            }

            for (int i = 0; i < nextWeapons.Count; i++)
            {
                Rectangle cardRect = new(panelRect.X + 16 + (i * 384), panelRect.Y + 48, 362, 252);
                DrawShopWeaponCard(g, nextWeapons[i], cardRect, profile.Coins, mouseLocation);
            }
        }

        private void DrawShopWeaponCard(
            Graphics g,
            WeaponDefinition weapon,
            Rectangle rect,
            int playerCoins,
            Point mouseLocation)
        {
            Color catColor = UiHelpers.GetCategoryColor(weapon.Category);
            Color catDark = UiHelpers.DarkenColor(catColor, 0.72f);
            bool canAfford = playerCoins >= weapon.UnlockCost;
            Rectangle buyBtn = new(rect.X + 14, rect.Bottom - 54, rect.Width - 28, 40);
            bool hoverBuy = buyBtn.Contains(mouseLocation);
            bool hoverCard = rect.Contains(mouseLocation);

            Rectangle drawRect = hoverCard && canAfford ? rect with { Y = rect.Y - 3 } : rect;
            Rectangle shadowRect = drawRect with { X = drawRect.X + 4, Y = drawRect.Y + 5 };
            UiHelpers.DrawRoundedBox(g, shadowRect, 10, Color.FromArgb(115, 0, 0, 0), Color.Transparent, 0f);
            UiHelpers.DrawGradientRoundedBox(
                g,
                drawRect,
                10,
                Color.FromArgb(39, 48, 64),
                Color.FromArgb(27, 34, 46),
                hoverCard ? catColor : UiTheme.Border,
                hoverCard ? 2.2f : 1.5f);

            UiHelpers.DrawCardHeader(g, drawRect, 42, 8, catColor, catDark);
            EntityGraphics.DrawCategoryIcon(g, weapon.Category, drawRect.X + 20, drawRect.Y + 23, 14);
            DrawShopWeaponCardContent(g, weapon, drawRect);

            Rectangle drawBuyBtn = new(drawRect.X + 14, drawRect.Bottom - 54, drawRect.Width - 28, 40);
            if (canAfford)
            {
                UiHelpers.DrawGradientButton(
                    g,
                    drawBuyBtn,
                    hoverBuy ? Color.FromArgb(84, 196, 130) : Color.FromArgb(62, 166, 107),
                    hoverBuy ? Color.FromArgb(58, 156, 98) : Color.FromArgb(44, 128, 80),
                    Color.FromArgb(118, 224, 158),
                    $"UNLOCK WEAPON ({weapon.UnlockCost} COINS)",
                    10f,
                    Color.White);
            }
            else
            {
                UiHelpers.DrawDisabledButton(g, drawBuyBtn, 7, $"NEED {weapon.UnlockCost} COINS", 9.8f);
            }

            AddHitbox(buyBtn, MenuActionType.BuyWeapon, weapon.Id, true);
        }

        private static void DrawShopWeaponCardContent(Graphics g, WeaponDefinition weapon, Rectangle drawRect)
        {
            using Font nameFont = new("Segoe UI", 11.8f, FontStyle.Bold);
            using Font badgeFont = new("Segoe UI", 8.5f, FontStyle.Bold);
            using Font descFont = new("Segoe UI", 9.5f, FontStyle.Regular);
            using SolidBrush whiteBrush = new(Color.White);
            using SolidBrush primaryBrush = new(UiTheme.TextPrimary);
            using StringFormat sf = UiHelpers.CreateStringFormat();

            g.DrawString(weapon.Name, nameFont, whiteBrush, drawRect.X + 34, drawRect.Y + 11);

            Rectangle catPill = new(drawRect.Right - 96, drawRect.Y + 11, 84, 24);
            UiHelpers.DrawRoundedBox(g, catPill, 5, Color.FromArgb(90, 0, 0, 0), Color.FromArgb(140, 255, 255, 255), 1f);
            g.DrawString(weapon.Category.ToString().ToUpperInvariant(), badgeFont, whiteBrush, catPill, sf);

            UiHelpers.DrawStatPill(g, new Rectangle(drawRect.X + 14, drawRect.Y + 54, 102, 28), $"DMG: {weapon.BaseDamage}", Color.FromArgb(228, 104, 96));
            UiHelpers.DrawStatPill(g, new Rectangle(drawRect.X + 124, drawRect.Y + 54, 110, 28), $"CD: {weapon.BaseCooldown} Turns", UiTheme.Cyan);
            UiHelpers.DrawStatPill(g, new Rectangle(drawRect.X + 242, drawRect.Y + 54, 106, 28), weapon.Targeting.ToString(), UiTheme.Gold);

            Rectangle descBox = new(drawRect.X + 14, drawRect.Y + 94, drawRect.Width - 28, 90);
            UiHelpers.DrawRoundedBox(g, descBox, 6, Color.FromArgb(24, 30, 41), Color.FromArgb(46, 56, 73), 1f);
            g.DrawString(weapon.Description, descFont, primaryBrush, new Rectangle(descBox.X + 10, descBox.Y + 8, descBox.Width - 20, descBox.Height - 16));
        }

        private void DrawUpgradesPanel(
            Graphics g,
            GameDatabase database,
            PlayerProfile profile,
            Point mouseLocation)
        {
            Rectangle panelRect = new(474, 412, 778, 262);
            UiHelpers.DrawRoundedBox(g, panelRect, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            int unlockedCount = database.UpgradeList.Count(u => profile.UnlockedUpgradeIds.Contains(u.Id));
            DrawShopPanelHeader(g, panelRect, "PERMANENT UPGRADES — TRAINING QUEUE", unlockedCount, database.UpgradeList.Count);

            List<PermanentUpgradeDefinition> nextUpgrades = database.UpgradeList
                .Where(u => !profile.UnlockedUpgradeIds.Contains(u.Id))
                .OrderBy(u => u.UnlockOrder)
                .Take(2)
                .ToList();

            if (nextUpgrades.Count == 0)
            {
                DrawAllUnlockedBanner(g, panelRect, "ALL PERMANENT UPGRADES MASTERED!", UiTheme.Gold);
                return;
            }

            for (int i = 0; i < nextUpgrades.Count; i++)
            {
                Rectangle cardRect = new(panelRect.X + 16 + (i * 384), panelRect.Y + 48, 362, 198);
                DrawShopUpgradeCard(g, nextUpgrades[i], cardRect, profile.Coins, mouseLocation);
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
            Rectangle buyBtn = new(rect.X + 14, rect.Bottom - 52, rect.Width - 28, 38);
            bool hoverBuy = buyBtn.Contains(mouseLocation);
            bool hoverCard = rect.Contains(mouseLocation);

            UiHelpers.DrawGradientRoundedBox(
                g,
                rect,
                10,
                Color.FromArgb(39, 48, 64),
                Color.FromArgb(27, 34, 46),
                hoverCard ? UiTheme.Gold : UiTheme.Border,
                hoverCard ? 2f : 1.5f);

            UiHelpers.DrawCardHeader(g, rect, 40, 8, Color.FromArgb(46, 56, 74), Color.FromArgb(46, 56, 74));

            using (Font nameFont = new("Segoe UI", 11.5f, FontStyle.Bold))
            using (Font tierFont = new("Segoe UI", 8.5f, FontStyle.Bold))
            using (Font descFont = new("Segoe UI", 9.8f, FontStyle.Regular))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            using (SolidBrush textBrush = new(UiTheme.TextPrimary))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                g.DrawString(upgrade.Name, nameFont, goldBrush, rect.X + 14, rect.Y + 10);

                Rectangle tierPill = new(rect.Right - 76, rect.Y + 10, 64, 22);
                UiHelpers.DrawRoundedBox(g, tierPill, 5, Color.FromArgb(30, 37, 50), UiTheme.GoldDark, 1f);
                g.DrawString($"TIER #{upgrade.UnlockOrder}", tierFont, goldBrush, tierPill, sf);

                Rectangle descBox = new(rect.X + 14, rect.Y + 52, rect.Width - 28, 82);
                UiHelpers.DrawRoundedBox(g, descBox, 6, Color.FromArgb(24, 30, 41), Color.FromArgb(46, 56, 73), 1f);
                g.DrawString(upgrade.Description, descFont, textBrush, new Rectangle(descBox.X + 10, descBox.Y + 10, descBox.Width - 20, descBox.Height - 20));
            }

            if (canAfford)
            {
                UiHelpers.DrawGradientButton(
                    g,
                    buyBtn,
                    hoverBuy ? Color.FromArgb(236, 184, 72) : Color.FromArgb(204, 150, 44),
                    hoverBuy ? Color.FromArgb(196, 140, 34) : Color.FromArgb(164, 114, 24),
                    UiTheme.Gold,
                    $"PURCHASE UPGRADE ({upgrade.Cost} COINS)",
                    9.8f,
                    UiTheme.DarkButtonText);
            }
            else
            {
                UiHelpers.DrawDisabledButton(g, buyBtn, 7, $"NEED {upgrade.Cost} COINS", 9.8f);
            }

            AddHitbox(buyBtn, MenuActionType.BuyUpgrade, upgrade.Id, true);
        }

        private static void DrawShopPanelHeader(Graphics g, Rectangle panelRect, string title, int unlocked, int total)
        {
            using Font titleFont = new("Segoe UI", 11.5f, FontStyle.Bold);
            using Font countFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush mutedBrush = new(UiTheme.TextSecondary);
            using StringFormat rightFormat = UiHelpers.CreateStringFormat(StringAlignment.Far, StringAlignment.Near);

            g.DrawString(title, titleFont, goldBrush, panelRect.X + 16, panelRect.Y + 13);
            g.DrawString(
                $"Unlocked: {unlocked} / {total}",
                countFont,
                mutedBrush,
                new Rectangle(panelRect.Right - 220, panelRect.Y + 15, 204, 24),
                rightFormat);
        }

        private static void DrawAllUnlockedBanner(Graphics g, Rectangle panelRect, string text, Color accent)
        {
            Rectangle doneRect = new(panelRect.X + 20, panelRect.Y + 52, panelRect.Width - 40, panelRect.Height - 72);
            UiHelpers.DrawRoundedBox(g, doneRect, 10, UiTheme.CardBg, accent, 1.5f);
            using Font doneFont = new("Segoe UI", 14f, FontStyle.Bold);
            using SolidBrush brush = new(accent);
            using StringFormat sf = UiHelpers.CreateStringFormat();
            g.DrawString(text, doneFont, brush, doneRect, sf);
        }

        private static void DrawFooterBar(Graphics g, GameDatabase database, string statusMessage)
        {
            Rectangle footerRect = new(0, UiTheme.WindowHeight - 36, UiTheme.WindowWidth, 36);
            using (SolidBrush barBrush = new(UiTheme.Header))
            {
                g.FillRectangle(barBrush, footerRect);
            }

            using (Pen sepPen = new(UiTheme.Border, 1f))
            {
                g.DrawLine(sepPen, 0, footerRect.Y, UiTheme.WindowWidth, footerRect.Y);
            }

            using Font footerFont = new("Segoe UI", 9f, FontStyle.Regular);
            using SolidBrush textBrush = new(UiTheme.TextSecondary);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using StringFormat leftFormat = UiHelpers.CreateStringFormat(StringAlignment.Near);
            using StringFormat rightFormat = UiHelpers.CreateStringFormat(StringAlignment.Far);

            string message = string.IsNullOrWhiteSpace(statusMessage)
                ? $"Loaded Data: {database.WeaponList.Count} Weapons, {database.Enemies.Count} Enemies, {database.Waves.Count} Waves, {database.UpgradeList.Count} Upgrades, {database.AchievementList.Count} Achievements."
                : statusMessage;

            g.DrawString(message, footerFont, string.IsNullOrWhiteSpace(statusMessage) ? textBrush : goldBrush, footerRect with { X = 28, Width = 760 }, leftFormat);
            g.DrawString("Single-Screen Tactical UI | Pure Procedural GDI+ Vector Art", footerFont, textBrush, footerRect with { X = 800, Width = UiTheme.WindowWidth - 828 }, rightFormat);
        }

        private void AddHitbox(Rectangle bounds, MenuActionType actionType, string targetId, bool isEnabled)
        {
            _hitboxes.Add(new MenuHitbox
            {
                Bounds = bounds,
                ActionType = actionType,
                TargetId = targetId,
                IsEnabled = isEnabled
            });
        }

        private static int CalculateMaxHpBonus(GameDatabase database, PlayerProfile profile) =>
            database.UpgradeList
                .Where(u => u.EffectType == PermanentUpgradeEffectType.MaxHp && profile.UnlockedUpgradeIds.Contains(u.Id))
                .Sum(u => u.EffectValue);
    }
}