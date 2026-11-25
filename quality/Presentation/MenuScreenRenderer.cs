using System.Collections.Generic;
using System.Drawing;
using RogueCards.Application;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public sealed class MenuScreenRenderer : IScreenRenderer
    {
        public void Render(Graphics g, UiRenderState state)
        {
            UiPrimitives.DrawScreenBackground(g);
            DrawHeader(g, state);
            string warning = !string.IsNullOrWhiteSpace(state.BannerMessage)
            ? state.BannerMessage
            : state.Engine.StartupWarning;

            if (!string.IsNullOrWhiteSpace(warning))
            {
                UiPrimitives.DrawWarningBanner(g, new Rectangle(32, 82, 1216, 28), warning);
            }

            DrawCharacterPanel(g, state, new Rectangle(32, 118, 396, 304));
            DrawExpeditionPanel(g, state, new Rectangle(32, 434, 396, 254));
            DrawWeaponShopPanel(g, state, new Rectangle(446, 118, 802, 278));
            DrawUpgradeShopPanel(g, state, new Rectangle(446, 408, 802, 280));

            UiPrimitives.DrawAchievementToast(g, state.Engine.Achievements.ActiveToast);
        }

        private static void DrawHeader(Graphics g, UiRenderState state)
        {
            UiPrimitives.DrawTopHeaderBar(g, "ROGUE CARDS", "Turn-Based 5x5 Tactical Roguelike Deckbuilder");

            GameEngine engine = state.Engine;
            UiPrimitives.DrawStatPill(
                g,
                new Rectangle(640, 14, 170, 46),
                "COINS",
                $"{engine.Profile.Coins} c",
                UiTheme.AccentGold,
                UiTheme.BorderGold);

            int completed = engine.Profile.CompletedAchievementIds.Count;
            int total = engine.Database.AchievementList.Count;
            UiPrimitives.DrawStatPill(
                g,
                new Rectangle(824, 14, 186, 46),
                "ACHIEVEMENTS",
                $"{completed} / {total}",
                UiTheme.ShieldCyan);

            UiPrimitives.DrawButton(
                g,
                state,
                new Rectangle(1024, 16, 224, 42),
                "Achievements & Stats",
                "OpenStats",
                accentTop: UiTheme.AccentPurpleTop,
                accentBottom: UiTheme.AccentPurpleBottom);
        }

        private static void DrawCharacterPanel(Graphics g, UiRenderState state, Rectangle panel)
        {
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);
            using SolidBrush sectionBrush = new SolidBrush(UiTheme.AccentGold);
            g.DrawString("SELECT HERO", UiTheme.FontSection, sectionBrush, panel.X + 16, panel.Y + 12);

            GameEngine engine = state.Engine;
            int bonusHp = engine.HasPermanentUpgrade(PermanentUpgradeType.DurabilityTraining) ? 1 : 0;
            int cardY = panel.Y + 44;

            foreach (CharacterDefinition character in engine.Database.CharacterList)
            {
                Rectangle cardRect = new Rectangle(panel.X + 14, cardY, panel.Width - 28, 118);
                DrawCharacterCard(g, state, character, bonusHp, cardRect);
                cardY += 128;
            }
        }

        private static void DrawCharacterCard(
            Graphics g,
            UiRenderState state,
            CharacterDefinition character,
            int bonusHp,
            Rectangle rect)
        {
            GameEngine engine = state.Engine;
            bool unlocked = engine.Profile.UnlockedCharacterIds.Contains(character.Id);
            bool selected = unlocked && string.Equals(engine.Profile.SelectedCharacterId, character.Id, System.StringComparison.OrdinalIgnoreCase);
            bool hovered = unlocked && state.IsHovered(rect);

            Color fill = selected
                ? Color.FromArgb(36, 54, 84)
                : (unlocked ? UiTheme.PanelBgAlt : UiTheme.CardDimBg);
            Color border = selected
                ? UiTheme.BorderGold
                : (hovered ? UiTheme.BorderHighlight : UiTheme.BorderSubtle);

            UiPrimitives.FillRoundedRect(g, rect, 8, fill, border, selected ? 2.2f : 1.3f);

            using SolidBrush nameBrush = new SolidBrush(unlocked ? UiTheme.TextPrimary : UiTheme.TextMuted);
            using SolidBrush statBrush = new SolidBrush(unlocked ? UiTheme.ShieldCyan : UiTheme.TextMuted);
            using SolidBrush descBrush = new SolidBrush(unlocked ? UiTheme.TextSecondary : UiTheme.TextMuted);

            string statusTag = selected ? "SELECTED" : (unlocked ? "READY" : "LOCKED");
            Color tagColor = selected ? UiTheme.AccentGold : (unlocked ? UiTheme.PoisonGreen : UiTheme.MeleeHeader);
            using SolidBrush tagBrush = new SolidBrush(tagColor);

            g.DrawString(character.Name, UiTheme.FontSection, nameBrush, rect.X + 12, rect.Y + 10);
            g.DrawString(
                statusTag,
                UiTheme.FontBadge,
                tagBrush,
                new Rectangle(rect.X + 12, rect.Y + 12, rect.Width - 24, 18),
                UiTheme.RightCenterFormat);

            int maxHp = character.BaseMaxHp + bonusHp;
            g.DrawString(
                $"HP: {maxHp}  |  Special: {character.SpecialName} (CD {character.SpecialCooldown})",
                UiTheme.FontSmallBold,
                statBrush,
                rect.X + 12,
                rect.Y + 34);

            string body = unlocked
                ? character.Description
                : "Locked — Complete the 'Kingslayer' achievement (Defeat Viking Chief on Wave 10).";
            g.DrawString(
                body,
                UiTheme.FontSmall,
                descBrush,
                new Rectangle(rect.X + 12, rect.Y + 55, rect.Width - 24, 56),
                UiTheme.TopLeftWrapFormat);

            state.RegisterHit("SelectCharacter", rect, character.Id, enabled: unlocked);
        }

        private static void DrawExpeditionPanel(Graphics g, UiRenderState state, Rectangle panel)
        {
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);
            using SolidBrush sectionBrush = new SolidBrush(UiTheme.AccentGold);
            g.DrawString("EXPEDITION", UiTheme.FontSection, sectionBrush, panel.X + 16, panel.Y + 12);

            GameEngine engine = state.Engine;
            Rectangle area1Box = new Rectangle(panel.X + 14, panel.Y + 42, 178, 56);
            Rectangle area2Box = new Rectangle(panel.X + 204, panel.Y + 42, 178, 56);

            UiPrimitives.DrawStatPill(g, area1Box, "LEVEL 1 (ACTIVE)", "10 Waves", UiTheme.TextPrimary, UiTheme.BorderHighlight);
            UiPrimitives.DrawStatPill(
                g,
                area2Box,
                "AREA 2 STATUS",
                engine.Profile.Area2Unlocked ? "UNLOCKED" : "LOCKED",
                engine.Profile.Area2Unlocked ? UiTheme.PoisonGreen : UiTheme.TextMuted,
                engine.Profile.Area2Unlocked ? UiTheme.PoisonGreen : UiTheme.BorderSubtle);

            UiPrimitives.DrawButton(
                g,
                state,
                new Rectangle(panel.X + 14, panel.Y + 114, panel.Width - 28, 48),
                "NEW RUN",
                "NewRun",
                accentTop: UiTheme.AccentGreenTop,
                accentBottom: UiTheme.AccentGreenBottom,
                font: UiTheme.FontSection);

            bool hasSave = engine.HasActiveRunSave();
            UiPrimitives.DrawButton(
                g,
                state,
                new Rectangle(panel.X + 14, panel.Y + 172, panel.Width - 28, 42),
                hasSave ? "CONTINUE SAVED RUN" : "NO SAVED RUN",
                "ContinueRun",
                enabled: hasSave,
                accentTop: UiTheme.AccentBlueTop,
                accentBottom: UiTheme.AccentBlueBottom);

            using SolidBrush footerBrush = new SolidBrush(UiTheme.TextMuted);
            string summary = $"Unlocked Weapons: {engine.Profile.UnlockedWeaponIds.Count}/{engine.Database.WeaponList.Count}  |  Permanent Upgrades: {engine.Profile.UnlockedUpgradeIds.Count}/{engine.Database.UpgradeList.Count}";
            g.DrawString(
                summary,
                UiTheme.FontBadge,
                footerBrush,
                new Rectangle(panel.X + 14, panel.Y + 222, panel.Width - 28, 22),
                UiTheme.CenterFormat);
        }

        private static void DrawWeaponShopPanel(Graphics g, UiRenderState state, Rectangle panel)
        {
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);
            using SolidBrush sectionBrush = new SolidBrush(UiTheme.AccentGold);
            g.DrawString("ARMORY — NEXT UNLOCKABLE WEAPONS", UiTheme.FontSection, sectionBrush, panel.X + 16, panel.Y + 12);

            IReadOnlyList<WeaponDefinition> weapons = state.Engine.GetNextUnlockableWeapons(2);
            if (weapons.Count == 0)
            {
                using SolidBrush doneBrush = new SolidBrush(UiTheme.PoisonGreen);
                g.DrawString(
                    "All 10 weapons are unlocked and available in your run drafts!",
                    UiTheme.FontSection,
                    doneBrush,
                    new Rectangle(panel.X + 20, panel.Y + 60, panel.Width - 40, 180),
                    UiTheme.CenterFormat);
                return;
            }

            int cardWidth = 376;
            int cardHeight = 166;
            int startX = panel.X + 16;

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponDefinition weapon = weapons[i];
                int x = startX + (i * (cardWidth + 18));
                Rectangle cardRect = new Rectangle(x, panel.Y + 44, cardWidth, cardHeight);

                UiPrimitives.DrawCardFace(
                    g,
                    cardRect,
                    weapon,
                    weapon.BaseDamage,
                    weapon.BaseCooldown,
                    0,
                    null,
                    false,
                    state.IsHovered(cardRect),
                    false,
                    false,
                    false,
                    $"Range: {weapon.Range}  |  Targeting: {weapon.Targeting}");

                bool canAfford = state.Engine.Profile.Coins >= weapon.UnlockCost;
                Rectangle buyRect = new Rectangle(x, cardRect.Bottom + 10, cardWidth, 38);
                UiPrimitives.DrawButton(
                    g,
                    state,
                    buyRect,
                    $"Unlock {weapon.Name} ({weapon.UnlockCost} Coins)",
                    "BuyWeapon",
                    payload: weapon.Id,
                    enabled: canAfford,
                    accentTop: UiTheme.AccentGold,
                    accentBottom: Color.FromArgb(166, 126, 42));
            }
        }

        private static void DrawUpgradeShopPanel(Graphics g, UiRenderState state, Rectangle panel)
        {
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);
            using SolidBrush sectionBrush = new SolidBrush(UiTheme.AccentGold);
            g.DrawString("TRAINING GROUNDS — NEXT PERMANENT UPGRADES", UiTheme.FontSection, sectionBrush, panel.X + 16, panel.Y + 12);

            IReadOnlyList<PermanentUpgradeDefinition> upgrades = state.Engine.GetNextUnlockableUpgrades(2);
            if (upgrades.Count == 0)
            {
                using SolidBrush doneBrush = new SolidBrush(UiTheme.PoisonGreen);
                g.DrawString(
                    "All 7 permanent training upgrades have been mastered!",
                    UiTheme.FontSection,
                    doneBrush,
                    new Rectangle(panel.X + 20, panel.Y + 60, panel.Width - 40, 180),
                    UiTheme.CenterFormat);
                return;
            }

            int cardWidth = 376;
            int cardHeight = 164;
            int startX = panel.X + 16;

            for (int i = 0; i < upgrades.Count; i++)
            {
                PermanentUpgradeDefinition upgrade = upgrades[i];
                int x = startX + (i * (cardWidth + 18));
                Rectangle boxRect = new Rectangle(x, panel.Y + 44, cardWidth, cardHeight);

                UiPrimitives.FillRoundedRect(g, boxRect, 9, UiTheme.CardBg, UiTheme.BorderSubtle, 1.5f);

                Rectangle headerRect = new Rectangle(boxRect.X + 1, boxRect.Y + 1, boxRect.Width - 2, 32);
                UiPrimitives.FillRoundedRect(g, headerRect, 7, UiTheme.AccentPurpleBottom);

                using SolidBrush titleBrush = new SolidBrush(UiTheme.TextPrimary);
                using SolidBrush descBrush = new SolidBrush(UiTheme.TextSecondary);
                using SolidBrush tierBrush = new SolidBrush(UiTheme.AccentGold);

                g.DrawString(
                    upgrade.Name,
                    UiTheme.FontCardTitle,
                    titleBrush,
                    new Rectangle(headerRect.X + 10, headerRect.Y, headerRect.Width - 70, headerRect.Height),
                    UiTheme.LeftCenterFormat);
                g.DrawString(
                    $"Tier {upgrade.UnlockOrder}/7",
                    UiTheme.FontBadge,
                    tierBrush,
                    new Rectangle(headerRect.X + 10, headerRect.Y, headerRect.Width - 20, headerRect.Height),
                    UiTheme.RightCenterFormat);

                g.DrawString(
                    upgrade.Description,
                    UiTheme.FontBody,
                    descBrush,
                    new Rectangle(boxRect.X + 12, boxRect.Y + 46, boxRect.Width - 24, 100),
                    UiTheme.TopLeftWrapFormat);

                bool canAfford = state.Engine.Profile.Coins >= upgrade.Cost;
                Rectangle buyRect = new Rectangle(x, boxRect.Bottom + 10, cardWidth, 38);
                UiPrimitives.DrawButton(
                    g,
                    state,
                    buyRect,
                    $"Purchase ({upgrade.Cost} Coins)",
                    "BuyUpgrade",
                    payload: upgrade.Id,
                    enabled: canAfford,
                    accentTop: UiTheme.AccentPurpleTop,
                    accentBottom: UiTheme.AccentPurpleBottom);
            }
        }
    }
}