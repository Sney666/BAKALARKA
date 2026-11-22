#nullable disable
using System.Drawing.Drawing2D;

namespace RogueCards
{
    public enum ArenaActionType
    {
        MoveToTile,
        AimTile,
        WaitTurn,
        UseSpecial,
        PlayQueue,
        CancelAim,
        EquipHandCard,
        EquippedSlot,
        SaveAndExit,
        DraftToggleCard,
        DraftConfirm,
        RewardPickCard,
        RewardSelectModifier,
        RewardSelectTargetCard,
        RewardApplyUpgrade,
        RewardReroll,
        RewardSkip,
        GameOverNewRun,
        GameOverMenu
    }

    public class ArenaHitbox
    {
        public Rectangle Bounds { get; init; }
        public ArenaActionType ActionType { get; init; }
        public GridPos TilePos { get; init; }
        public int Index { get; init; }
        public string TargetId { get; init; }
        public bool IsEnabled { get; init; }
    }

    public class ArenaRenderer
    {
        private const int GridSize = GameEngine.GridSize;
        private const int TileSize = 78;
        private const int TileGap = 4;

        private readonly Rectangle _arenaFrameRect = new(424, 66, 432, 432);
        private readonly List<ArenaHitbox> _hitboxes = [];
        private readonly List<Rectangle> _equippedSlotRects = [];

        public ArenaHitbox HitTest(Point point) => UiHelpers.HitTest(_hitboxes, point, static h => h.Bounds);

        public int HitTestEquippedSlotIndex(Point point)
        {
            for (int i = 0; i < _equippedSlotRects.Count; i++)
            {
                if (_equippedSlotRects[i].Contains(point))
                {
                    return i;
                }
            }

            return -1;
        }

        public bool TryGetTileAtPoint(Point point, out GridPos tilePos)
        {
            tilePos = new GridPos(-1, -1);
            (int startX, int startY) = GetGridOrigin();

            for (int row = 0; row < GridSize; row++)
            {
                for (int col = 0; col < GridSize; col++)
                {
                    if (GetTileRect(startX, startY, col, row).Contains(point))
                    {
                        tilePos = new GridPos(col, row);
                        return true;
                    }
                }
            }

            return false;
        }

        public void RenderArena(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation,
            float specialAnimProgress,
            float telegraphPulsePhase,
            AimPreviewResult aimPreview,
            int dragSourceIndex,
            int dragHoverTargetIndex)
        {
            ResetFrameState();
            if (engine?.CurrentRun == null)
            {
                return;
            }

            UiHelpers.DrawBackground(g, UiTheme.BgBottom);
            DrawTopStatusBar(g, engine, profile, mouseLocation);
            DrawLeftTacticalPanel(g, engine);
            DrawArenaGrid(g, engine, mouseLocation, specialAnimProgress, telegraphPulsePhase, aimPreview);
            DrawRightCombatLogPanel(g, engine);
            DrawBottomDock(g, engine, mouseLocation, dragSourceIndex, dragHoverTargetIndex);

            if (dragSourceIndex >= 0 && dragSourceIndex < engine.CurrentRun.Player.EquippedQueue.Count)
            {
                CardInstance draggedCard = engine.CurrentRun.Player.EquippedQueue[dragSourceIndex];
                Rectangle ghostRect = new(mouseLocation.X - 56, mouseLocation.Y - 80, 112, 160);
                DrawEquippedCard(g, engine, draggedCard, ghostRect, dragSourceIndex, false, true, false);
            }
        }

        public void RenderGameOver(Graphics g, GameEngine engine, PlayerProfile profile, Point mouseLocation)
        {
            ResetFrameState();
            UiHelpers.DrawBackground(g, UiTheme.BgBottom);

            ActiveRunState run = engine.CurrentRun;
            bool isVictory = run is { IsVictory: true };
            Color accent = isVictory ? UiTheme.Gold : UiTheme.Red;

            Rectangle cardRect = new((UiTheme.WindowWidth / 2) - 340, 140, 680, 420);
            UiHelpers.DrawGradientRoundedBox(g, cardRect, 14, Color.FromArgb(32, 39, 52), Color.FromArgb(22, 27, 36), accent, 2.5f);

            using (Font titleFont = new("Segoe UI", 30f, FontStyle.Bold))
            using (Font subFont = new("Segoe UI", 12.5f, FontStyle.Regular))
            using (Font statFont = new("Segoe UI", 11.5f, FontStyle.Bold))
            using (SolidBrush accentBrush = new(accent))
            using (SolidBrush primaryBrush = new(UiTheme.TextPrimary))
            using (SolidBrush secondaryBrush = new(UiTheme.TextSecondary))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                g.DrawString(isVictory ? "VICTORY!" : "DEFEAT", titleFont, accentBrush, new Rectangle(cardRect.X + 20, cardRect.Y + 32, cardRect.Width - 40, 56), sf);
                g.DrawString(run?.GameOverReason ?? "Run ended.", subFont, primaryBrush, new Rectangle(cardRect.X + 40, cardRect.Y + 104, cardRect.Width - 80, 56), sf);

                Rectangle statsBox = new(cardRect.X + 60, cardRect.Y + 178, cardRect.Width - 120, 116);
                UiHelpers.DrawRoundedBox(g, statsBox, 10, Color.FromArgb(22, 28, 38), UiTheme.Border, 1.2f);

                int waveReached = run?.CurrentWave ?? 1;
                int turns = run?.TurnNumber ?? 1;
                string heroName = engine.GetActiveCharacterDefinition().Name;

                g.DrawString($"Hero: {heroName}   |   Wave Reached: {waveReached} / 10", statFont, secondaryBrush, statsBox with { Y = statsBox.Y + 18, Height = 34 }, sf);
                g.DrawString($"Turns Taken: {turns}   |   Total Coins: {profile.Coins}", statFont, accentBrush, statsBox with { Y = statsBox.Y + 58, Height = 34 }, sf);
            }

            DrawGameOverButtons(g, cardRect, mouseLocation);
        }

        private void DrawGameOverButtons(Graphics g, Rectangle cardRect, Point mouseLocation)
        {
            Rectangle retryBtn = new(cardRect.X + 60, cardRect.Bottom - 84, 260, 50);
            bool hoverRetry = retryBtn.Contains(mouseLocation);
            UiHelpers.DrawGradientButton(
                g,
                retryBtn,
                hoverRetry ? Color.FromArgb(238, 184, 72) : Color.FromArgb(206, 150, 42),
                hoverRetry ? Color.FromArgb(196, 140, 34) : Color.FromArgb(164, 112, 24),
                UiTheme.Gold,
                "START NEW RUN",
                11.5f,
                UiTheme.DarkButtonText);
            AddHitbox(retryBtn, ArenaActionType.GameOverNewRun, isEnabled: true);

            Rectangle menuBtn = new(cardRect.Right - 320, cardRect.Bottom - 84, 260, 50);
            bool hoverMenu = menuBtn.Contains(mouseLocation);
            UiHelpers.DrawRoundedBox(g, menuBtn, 8, hoverMenu ? Color.FromArgb(56, 68, 88) : Color.FromArgb(38, 46, 61), hoverMenu ? UiTheme.Cyan : UiTheme.Border, 1.5f);

            using Font btnFont = new("Segoe UI", 11.5f, FontStyle.Bold);
            using SolidBrush whiteBrush = new(UiTheme.TextPrimary);
            using StringFormat sf = UiHelpers.CreateStringFormat();
            g.DrawString("RETURN TO MENU", btnFont, whiteBrush, menuBtn, sf);
            AddHitbox(menuBtn, ArenaActionType.GameOverMenu, isEnabled: true);
        }

        public void RenderStartingDraft(Graphics g, GameEngine engine, PlayerProfile profile, Point mouseLocation)
        {
            ResetFrameState();
            if (engine?.CurrentRun?.PendingReward == null)
            {
                return;
            }

            UiHelpers.DrawBackground(g, UiTheme.BgBottom);
            DrawTopStatusBar(g, engine, profile, mouseLocation);

            PendingRewardState draft = engine.CurrentRun.PendingReward;
            int requiredCount = Math.Min(2, draft.DraftCardIds.Count);

            using (Font titleFont = new("Segoe UI", 22f, FontStyle.Bold))
            using (Font subFont = new("Segoe UI", 11.5f, FontStyle.Regular))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            using (SolidBrush mutedBrush = new(UiTheme.TextSecondary))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                g.DrawString("BUILD YOUR STARTING HAND", titleFont, goldBrush, new Rectangle(40, 74, UiTheme.WindowWidth - 80, 42), sf);
                g.DrawString(
                    $"Choose {requiredCount} cards from {draft.DraftCardIds.Count} unlocked cards before Wave 1 begins   (Selected: {draft.DraftSelectedIds.Count} / {requiredCount})",
                    subFont,
                    mutedBrush,
                    new Rectangle(40, 118, UiTheme.WindowWidth - 80, 28),
                    sf);
            }

            DrawDraftCardsRow(g, engine, draft, mouseLocation);
            DrawDraftConfirmButton(g, draft, requiredCount, mouseLocation);
        }

        private void DrawDraftCardsRow(Graphics g, GameEngine engine, PendingRewardState draft, Point mouseLocation)
        {
            int count = draft.DraftCardIds.Count;
            const int cardW = 276;
            const int cardH = 380;
            const int gap = 22;
            int startX = (UiTheme.WindowWidth - ((count * cardW) + (Math.Max(0, count - 1) * gap))) / 2;

            for (int i = 0; i < count; i++)
            {
                string weaponId = draft.DraftCardIds[i];
                if (!engine.Database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
                {
                    continue;
                }

                Rectangle cardRect = new(startX + (i * (cardW + gap)), 168, cardW, cardH);
                int selIndex = draft.DraftSelectedIds.IndexOf(weaponId);
                bool isSelected = selIndex >= 0;
                string actionLabel = isSelected ? $"SELECTED (#{selIndex + 1})" : "CLICK TO SELECT";

                DrawLargeWeaponCard(g, engine, weapon, null, cardRect, isSelected, cardRect.Contains(mouseLocation), actionLabel);
                AddHitbox(cardRect, ArenaActionType.DraftToggleCard, targetId: weaponId, isEnabled: true);
            }
        }

        private void DrawDraftConfirmButton(Graphics g, PendingRewardState draft, int requiredCount, Point mouseLocation)
        {
            bool canConfirm = draft.DraftSelectedIds.Count >= requiredCount;
            Rectangle confirmBtn = new((UiTheme.WindowWidth / 2) - 210, 586, 420, 56);
            bool hoverConfirm = confirmBtn.Contains(mouseLocation) && canConfirm;

            if (canConfirm)
            {
                UiHelpers.DrawGradientButton(
                    g,
                    confirmBtn,
                    hoverConfirm ? Color.FromArgb(240, 188, 76) : Color.FromArgb(208, 154, 44),
                    hoverConfirm ? Color.FromArgb(198, 142, 34) : Color.FromArgb(166, 116, 26),
                    UiTheme.Gold,
                    "CONFIRM STARTING HAND & BEGIN WAVE 1",
                    12f,
                    UiTheme.DarkButtonText);
            }
            else
            {
                UiHelpers.DrawDisabledButton(g, confirmBtn, 9, $"SELECT {requiredCount - draft.DraftSelectedIds.Count} MORE CARD(S)", 11.5f);
            }

            AddHitbox(confirmBtn, ArenaActionType.DraftConfirm, isEnabled: canConfirm);
        }

        public void RenderRewardCard(Graphics g, GameEngine engine, PlayerProfile profile, Point mouseLocation)
        {
            ResetFrameState();
            if (engine?.CurrentRun?.PendingReward == null)
            {
                return;
            }

            UiHelpers.DrawBackground(g, UiTheme.BgBottom);
            DrawTopStatusBar(g, engine, profile, mouseLocation);
            DrawRewardScreenTitle(g, "WAVE CLEARED — PICK A CARD", 72, 118, 480, 28, 22f, 10f);

            PendingRewardState reward = engine.CurrentRun.PendingReward;
            DrawRewardCardChoices(g, engine, reward, mouseLocation);
            DrawRewardFooterButtons(g, engine, profile, mouseLocation, (UiTheme.WindowWidth / 2) - 290, 276, (UiTheme.WindowWidth / 2) + 14, 276, 600);
        }

        private void DrawRewardCardChoices(Graphics g, GameEngine engine, PendingRewardState reward, Point mouseLocation)
        {
            int count = reward.DraftCardIds.Count;
            const int cardW = 340;
            const int cardH = 382;
            const int gap = 52;
            int startX = (UiTheme.WindowWidth - ((count * cardW) + (Math.Max(0, count - 1) * gap))) / 2;
            string[] headers = ["OFFENSE POOL (MELEE / RANGED)", "UTILITY POOL (MOVEMENT / SPECIAL)"];

            using Font poolFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using SolidBrush cyanBrush = new(UiTheme.Cyan);
            using StringFormat sf = UiHelpers.CreateStringFormat();

            for (int i = 0; i < count; i++)
            {
                string weaponId = reward.DraftCardIds[i];
                if (!engine.Database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
                {
                    continue;
                }

                Rectangle cardRect = new(startX + (i * (cardW + gap)), 178, cardW, cardH);
                if (i < headers.Length)
                {
                    g.DrawString(headers[i], poolFont, cyanBrush, new Rectangle(cardRect.X, 152, cardW, 22), sf);
                }

                DrawLargeWeaponCard(g, engine, weapon, null, cardRect, false, cardRect.Contains(mouseLocation), "ADD TO HAND");
                AddHitbox(cardRect, ArenaActionType.RewardPickCard, targetId: weaponId, isEnabled: true);
            }
        }

        public void RenderRewardUpgrade(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation,
            int selectedModifierIndex,
            int selectedCardInstanceId)
        {
            ResetFrameState();
            if (engine?.CurrentRun?.PendingReward == null)
            {
                return;
            }

            UiHelpers.DrawBackground(g, UiTheme.BgBottom);
            DrawTopStatusBar(g, engine, profile, mouseLocation);
            DrawRewardScreenTitle(g, "WAVE CLEARED — UPGRADE A CARD", 66, 106, 460, 24, 20f, 9.5f);

            PendingRewardState reward = engine.CurrentRun.PendingReward;
            DrawUpgradeModifiersRow(g, reward, mouseLocation, selectedModifierIndex);

            List<CardInstance> ownedCards = engine.GetAllOwnedCards();
            DrawUpgradeTargetCardsRow(g, engine, ownedCards, mouseLocation, selectedCardInstanceId);
            DrawApplyUpgradeButton(g, reward, ownedCards, selectedModifierIndex, selectedCardInstanceId, mouseLocation);
            DrawRewardFooterButtons(g, engine, profile, mouseLocation, (UiTheme.WindowWidth / 2) - 108, 270, (UiTheme.WindowWidth / 2) + 182, 248, 616);
        }

        private static void DrawRewardScreenTitle(
            Graphics g,
            string title,
            int titleY,
            int pillY,
            int pillW,
            int pillH,
            float titleSize,
            float badgeSize)
        {
            using Font titleFont = new("Segoe UI", titleSize, FontStyle.Bold);
            using Font badgeFont = new("Segoe UI", badgeSize, FontStyle.Bold);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush greenBrush = new(Color.FromArgb(128, 232, 166));
            using StringFormat sf = UiHelpers.CreateStringFormat();

            g.DrawString(title, titleFont, goldBrush, new Rectangle(40, titleY, UiTheme.WindowWidth - 80, 40), sf);
            Rectangle resetPill = new((UiTheme.WindowWidth / 2) - (pillW / 2), pillY, pillW, pillH);
            UiHelpers.DrawRoundedBox(g, resetPill, 7, Color.FromArgb(26, 56, 42), UiTheme.Green, 1.2f);
            g.DrawString("ALL CARD & SPECIAL COOLDOWNS RESET TO 0!", badgeFont, greenBrush, resetPill, sf);
        }

        private void DrawUpgradeModifiersRow(Graphics g, PendingRewardState reward, Point mouseLocation, int selectedModifierIndex)
        {
            using (Font stepFont = new("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush cyanBrush = new(UiTheme.Cyan))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                g.DrawString("STEP 1: CHOOSE AN UPGRADE MODIFIER", stepFont, cyanBrush, new Rectangle(40, 138, UiTheme.WindowWidth - 80, 22), sf);
            }

            int modCount = reward.UpgradeOptions.Count;
            const int modW = 350;
            const int modH = 114;
            const int modGap = 24;
            int startX = (UiTheme.WindowWidth - ((modCount * modW) + (Math.Max(0, modCount - 1) * modGap))) / 2;

            for (int i = 0; i < modCount; i++)
            {
                UpgradeOption option = reward.UpgradeOptions[i];
                Rectangle modRect = new(startX + (i * (modW + modGap)), 164, modW, modH);
                bool isSelected = i == selectedModifierIndex;
                bool isHovered = modRect.Contains(mouseLocation);

                UiHelpers.DrawGradientRoundedBox(
                    g,
                    modRect,
                    10,
                    isSelected ? Color.FromArgb(48, 62, 84) : Color.FromArgb(38, 47, 62),
                    isSelected ? Color.FromArgb(34, 44, 62) : Color.FromArgb(28, 35, 48),
                    isSelected ? UiTheme.Gold : (isHovered ? UiTheme.Cyan : UiTheme.Border),
                    isSelected ? 2.5f : 1.5f);

                int textOffsetX = 14;
                if (option.ModifierType == UpgradeModifierType.PoisonEffect)
                {
                    EntityGraphics.DrawPoisonVialIcon(g, modRect.X + 24, modRect.Y + 24, 18);
                    textOffsetX = 40;
                }

                using Font nameFont = new("Segoe UI", 12.5f, FontStyle.Bold);
                using Font descFont = new("Segoe UI", 9.2f, FontStyle.Regular);
                using SolidBrush titleBrush = new(option.ModifierType == UpgradeModifierType.PoisonEffect ? UiTheme.Green : UiTheme.Gold);
                using SolidBrush textBrush = new(UiTheme.TextPrimary);

                g.DrawString(option.Name, nameFont, titleBrush, modRect.X + textOffsetX, modRect.Y + 12);
                g.DrawString(option.Description, descFont, textBrush, new Rectangle(modRect.X + 14, modRect.Y + 42, modRect.Width - 28, 62));

                AddHitbox(modRect, ArenaActionType.RewardSelectModifier, index: i, isEnabled: true);
            }
        }

        private void DrawUpgradeTargetCardsRow(
            Graphics g,
            GameEngine engine,
            List<CardInstance> ownedCards,
            Point mouseLocation,
            int selectedCardInstanceId)
        {
            using (Font stepFont = new("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush cyanBrush = new(UiTheme.Cyan))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                g.DrawString("STEP 2: CHOOSE ONE OF YOUR CARDS TO ATTACH IT TO", stepFont, cyanBrush, new Rectangle(40, 292, UiTheme.WindowWidth - 80, 22), sf);
            }

            int cardCount = ownedCards.Count;
            const int cardW = 266;
            const int cardH = 268;
            const int cardGap = 18;
            int startX = (UiTheme.WindowWidth - ((cardCount * cardW) + (Math.Max(0, cardCount - 1) * cardGap))) / 2;

            for (int i = 0; i < cardCount; i++)
            {
                CardInstance card = ownedCards[i];
                Rectangle cardRect = new(startX + (i * (cardW + cardGap)), 320, cardW, cardH);
                bool isSelected = card.InstanceId == selectedCardInstanceId;
                DrawLargeWeaponCard(g, engine, card.Definition, card, cardRect, isSelected, cardRect.Contains(mouseLocation), isSelected ? "TARGET SELECTED" : "CLICK TO SELECT");
                AddHitbox(cardRect, ArenaActionType.RewardSelectTargetCard, index: card.InstanceId, isEnabled: true);
            }
        }

        private void DrawApplyUpgradeButton(
            Graphics g,
            PendingRewardState reward,
            List<CardInstance> ownedCards,
            int selectedModifierIndex,
            int selectedCardInstanceId,
            Point mouseLocation)
        {
            bool canApply = selectedModifierIndex >= 0 &&
                            selectedModifierIndex < reward.UpgradeOptions.Count &&
                            ownedCards.Any(c => c.InstanceId == selectedCardInstanceId);

            Rectangle applyBtn = new((UiTheme.WindowWidth / 2) - 430, 616, 300, 48);
            bool hoverApply = applyBtn.Contains(mouseLocation) && canApply;

            if (canApply)
            {
                UiHelpers.DrawGradientButton(
                    g,
                    applyBtn,
                    hoverApply ? Color.FromArgb(88, 202, 134) : Color.FromArgb(62, 166, 107),
                    hoverApply ? Color.FromArgb(56, 158, 98) : Color.FromArgb(44, 128, 80),
                    Color.FromArgb(124, 228, 162),
                    "APPLY UPGRADE",
                    11f,
                    Color.White);
            }
            else
            {
                UiHelpers.DrawDisabledButton(g, applyBtn, 8, "SELECT MODIFIER & CARD", 10.5f);
            }

            AddHitbox(applyBtn, ArenaActionType.RewardApplyUpgrade, isEnabled: canApply);
        }

        private void DrawRewardFooterButtons(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation,
            int rerollX,
            int rerollW,
            int skipX,
            int skipW,
            int y)
        {
            PendingRewardState reward = engine.CurrentRun.PendingReward;
            bool hasFreeReroll = engine.CurrentRun.Player.FreeRerollsRemaining > 0;
            bool canAfford = hasFreeReroll || profile.Coins >= reward.NextRerollCost;

            Rectangle rerollBtn = new(rerollX, y, rerollW, 48);
            bool hoverReroll = rerollBtn.Contains(mouseLocation) && canAfford;
            string rerollLabel = hasFreeReroll ? "REROLL (FREE — CHARISMA)" : $"REROLL ({reward.NextRerollCost} COINS)";

            if (canAfford)
            {
                UiHelpers.DrawGradientButton(
                    g,
                    rerollBtn,
                    hoverReroll ? Color.FromArgb(236, 184, 72) : Color.FromArgb(204, 150, 42),
                    hoverReroll ? Color.FromArgb(196, 140, 34) : Color.FromArgb(164, 112, 24),
                    UiTheme.Gold,
                    rerollLabel,
                    10f,
                    UiTheme.DarkButtonText);
            }
            else
            {
                UiHelpers.DrawDisabledButton(g, rerollBtn, 8, $"REROLL (NEED {reward.NextRerollCost} COINS)", 9.8f);
            }

            AddHitbox(rerollBtn, ArenaActionType.RewardReroll, isEnabled: canAfford);

            Rectangle skipBtn = new(skipX, y, skipW, 48);
            bool hoverSkip = skipBtn.Contains(mouseLocation);
            UiHelpers.DrawRoundedBox(g, skipBtn, 8, hoverSkip ? Color.FromArgb(56, 68, 88) : Color.FromArgb(38, 46, 61), hoverSkip ? UiTheme.Cyan : UiTheme.Border, 1.5f);

            using Font btnFont = new("Segoe UI", 10.5f, FontStyle.Bold);
            using SolidBrush whiteBrush = new(UiTheme.TextPrimary);
            using StringFormat sf = UiHelpers.CreateStringFormat();
            g.DrawString("SKIP REWARD", btnFont, whiteBrush, skipBtn, sf);
            AddHitbox(skipBtn, ArenaActionType.RewardSkip, isEnabled: true);
        }

        private static void DrawLargeWeaponCard(
            Graphics g,
            GameEngine engine,
            WeaponDefinition def,
            CardInstance instance,
            Rectangle rect,
            bool isSelected,
            bool isHovered,
            string footerActionLabel)
        {
            Color catColor = UiHelpers.GetCategoryColor(def.Category);
            Color catDark = UiHelpers.DarkenColor(catColor, 0.72f);

            Rectangle drawRect = isHovered || isSelected ? rect with { Y = rect.Y - 6 } : rect;
            Rectangle shadow = drawRect with { X = drawRect.X + 5, Y = drawRect.Y + 7 };
            UiHelpers.DrawRoundedBox(g, shadow, 12, Color.FromArgb(130, 0, 0, 0), Color.Transparent, 0f);
            UiHelpers.DrawGradientRoundedBox(
                g,
                drawRect,
                12,
                Color.FromArgb(39, 48, 64),
                Color.FromArgb(27, 34, 46),
                isSelected ? UiTheme.Gold : (isHovered ? catColor : UiTheme.Border),
                isSelected ? 3f : 1.8f);

            UiHelpers.DrawCardHeader(g, drawRect, 42, 10, catColor, catDark);
            EntityGraphics.DrawCategoryIcon(g, def.Category, drawRect.X + 20, drawRect.Y + 23, 15);

            CardInstance previewCard = instance ?? new CardInstance { Definition = def, DefinitionId = def.Id };
            int effectiveDmg = engine.GetEffectiveCardDamage(previewCard);
            int effectiveMaxCd = engine.GetEffectiveCardMaxCooldown(previewCard);

            using Font nameFont = new("Segoe UI", 11.2f, FontStyle.Bold);
            using Font badgeFont = new("Segoe UI", 8.2f, FontStyle.Bold);
            using Font descFont = new("Segoe UI", 9.3f, FontStyle.Regular);
            using SolidBrush whiteBrush = new(Color.White);
            using SolidBrush darkTextBrush = new(UiTheme.DarkButtonText);
            using SolidBrush primaryBrush = new(UiTheme.TextPrimary);
            using SolidBrush greenBrush = new(Color.FromArgb(132, 234, 166));
            using StringFormat centerSf = UiHelpers.CreateStringFormat();

            g.DrawString(def.Name, nameFont, whiteBrush, drawRect.X + 34, drawRect.Y + 12);

            Rectangle catPill = new(drawRect.Right - 86, drawRect.Y + 11, 74, 22);
            UiHelpers.DrawRoundedBox(g, catPill, 5, Color.FromArgb(95, 0, 0, 0), Color.FromArgb(150, 255, 255, 255), 1f);
            g.DrawString(def.Category.ToString().ToUpperInvariant(), badgeFont, whiteBrush, catPill, centerSf);

            int pillY = drawRect.Y + 54;
            int pillW = (drawRect.Width - 36) / 3;
            UiHelpers.DrawStatPill(g, new Rectangle(drawRect.X + 12, pillY, pillW, 28), $"DMG: {effectiveDmg}", Color.FromArgb(236, 114, 106), 8.2f);
            UiHelpers.DrawStatPill(g, new Rectangle(drawRect.X + 18 + pillW, pillY, pillW, 28), $"CD: {effectiveMaxCd}T", UiTheme.Cyan, 8.2f);
            UiHelpers.DrawStatPill(g, new Rectangle(drawRect.X + 24 + (pillW * 2), pillY, pillW, 28), def.Targeting.ToString(), UiTheme.Gold, 8.2f);

            int descTop = pillY + 38;
            if (instance != null && instance.AppliedModifiers.Count > 0)
            {
                Rectangle modRect = new(drawRect.X + 12, descTop, drawRect.Width - 24, 24);
                UiHelpers.DrawRoundedBox(g, modRect, 5, Color.FromArgb(28, 48, 40), UiTheme.Green, 1f);
                if (instance.HasPoison)
                {
                    EntityGraphics.DrawPoisonVialIcon(g, modRect.X + 14, modRect.Y + 12, 13);
                }

                g.DrawString($"Mods: {string.Join(", ", instance.AppliedModifiers)}", badgeFont, greenBrush, modRect, centerSf);
                descTop += 30;
            }

            int descHeight = Math.Max(60, (drawRect.Bottom - 56) - descTop);
            Rectangle descBox = new(drawRect.X + 12, descTop, drawRect.Width - 24, descHeight);
            UiHelpers.DrawRoundedBox(g, descBox, 7, Color.FromArgb(22, 28, 38), Color.FromArgb(46, 56, 73), 1f);
            g.DrawString(def.Description, descFont, primaryBrush, new Rectangle(descBox.X + 10, descBox.Y + 8, descBox.Width - 20, descBox.Height - 16));

            Rectangle footerBtn = new(drawRect.X + 12, drawRect.Bottom - 44, drawRect.Width - 24, 32);
            UiHelpers.DrawRoundedBox(g, footerBtn, 6, isSelected ? Color.FromArgb(196, 144, 40) : Color.FromArgb(44, 54, 72), isSelected ? UiTheme.Gold : catColor, 1.3f);
            g.DrawString(footerActionLabel, badgeFont, isSelected ? darkTextBrush : whiteBrush, footerBtn, centerSf);
        }

        private void DrawTopStatusBar(Graphics g, GameEngine engine, PlayerProfile profile, Point mouseLocation)
        {
            ActiveRunState run = engine.CurrentRun;
            PlayerState player = run.Player;
            CharacterDefinition hero = engine.GetActiveCharacterDefinition();

            UiHelpers.DrawHeaderBar(g, 56);
            DrawTopBarHeroAndHp(g, hero, player);
            DrawTopBarRunMetrics(g, engine, profile, run);

            Rectangle exitBtn = new(1086, 10, 174, 36);
            bool hoverExit = exitBtn.Contains(mouseLocation);
            UiHelpers.DrawRoundedBox(g, exitBtn, 7, hoverExit ? Color.FromArgb(58, 70, 92) : Color.FromArgb(38, 46, 61), hoverExit ? UiTheme.Gold : UiTheme.Border, 1.5f);

            using Font btnFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using SolidBrush whiteBrush = new(UiTheme.TextPrimary);
            using StringFormat sf = UiHelpers.CreateStringFormat();
            g.DrawString("Save & Exit to Menu", btnFont, whiteBrush, exitBtn, sf);
            AddHitbox(exitBtn, ArenaActionType.SaveAndExit, isEnabled: true);
        }

        private static void DrawTopBarHeroAndHp(Graphics g, CharacterDefinition hero, PlayerState player)
        {
            Rectangle heroPill = new(20, 10, 196, 36);
            UiHelpers.DrawRoundedBox(g, heroPill, 7, Color.FromArgb(32, 42, 58), UiTheme.Gold, 1.5f);
            using (Font heroFont = new("Segoe UI", 11f, FontStyle.Bold))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                g.DrawString(hero.Name.ToUpperInvariant(), heroFont, goldBrush, heroPill, sf);
            }

            Rectangle hpBox = new(230, 10, 214, 36);
            UiHelpers.DrawRoundedBox(g, hpBox, 7, Color.FromArgb(30, 36, 48), UiTheme.Border, 1.2f);
            float hpRatio = player.MaxHp > 0 ? Math.Clamp((float)player.CurrentHp / player.MaxHp, 0f, 1f) : 0f;
            Rectangle hpBarBg = new(hpBox.X + 10, hpBox.Y + 8, 114, 20);
            UiHelpers.DrawRoundedBox(g, hpBarBg, 4, Color.FromArgb(46, 26, 30), Color.FromArgb(95, 42, 46), 1f);

            if (hpRatio > 0f)
            {
                int fillW = Math.Max(8, (int)(hpBarBg.Width * hpRatio));
                Rectangle hpBarFill = new(hpBarBg.X + 1, hpBarBg.Y + 1, fillW - 2, hpBarBg.Height - 2);
                UiHelpers.DrawGradientRoundedBox(g, hpBarFill, 3, Color.FromArgb(236, 92, 86), Color.FromArgb(174, 42, 42), Color.Transparent, 0f);
            }

            using Font statFont = new("Segoe UI", 9.8f, FontStyle.Bold);
            using SolidBrush whiteBrush = new(UiTheme.TextPrimary);
            using StringFormat centerSf = UiHelpers.CreateStringFormat();
            g.DrawString($"HP {player.CurrentHp}/{player.MaxHp}", statFont, whiteBrush, hpBox with { X = hpBarBg.Right + 6, Width = 80 }, centerSf);
        }

        private static void DrawTopBarRunMetrics(Graphics g, GameEngine engine, PlayerProfile profile, ActiveRunState run)
        {
            Rectangle coinBox = new(458, 10, 148, 36);
            UiHelpers.DrawRoundedBox(g, coinBox, 7, Color.FromArgb(38, 34, 24), UiTheme.Gold, 1.2f);
            EntityGraphics.DrawCoinIcon(g, coinBox.X + 20, coinBox.Y + 18, 9);
            using (Font coinFont = new("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            using (StringFormat leftSf = UiHelpers.CreateStringFormat(StringAlignment.Near))
            {
                g.DrawString($"{profile.Coins} Coins", coinFont, goldBrush, coinBox with { X = coinBox.X + 36, Width = 106 }, leftSf);
            }

            Rectangle waveBox = new(620, 10, 246, 36);
            UiHelpers.DrawRoundedBox(g, waveBox, 7, Color.FromArgb(30, 38, 52), UiTheme.Cyan, 1.2f);
            using (Font waveFont = new("Segoe UI", 10.2f, FontStyle.Bold))
            using (SolidBrush cyanBrush = new(UiTheme.Cyan))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                g.DrawString($"WAVE {run.CurrentWave} / 10   |   TURN {run.TurnNumber}", waveFont, cyanBrush, waveBox, sf);
            }

            Rectangle upgradeBox = new(880, 10, 192, 36);
            int activeUpgrades = engine.GetActiveUpgradeNames().Count;
            UiHelpers.DrawRoundedBox(g, upgradeBox, 7, Color.FromArgb(30, 36, 48), UiTheme.Border, 1.2f);
            using Font upgFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using SolidBrush upgBrush = new(activeUpgrades > 0 ? UiTheme.Gold : UiTheme.TextSecondary);
            using StringFormat centerSf = UiHelpers.CreateStringFormat();
            g.DrawString($"Upgrades Active: {activeUpgrades}", upgFont, upgBrush, upgradeBox, centerSf);
        }

        private static void DrawLeftTacticalPanel(Graphics g, GameEngine engine)
        {
            DrawHeroBonusPanel(g, engine);
            DrawLegendPanel(g);
            DrawControlsPanel(g);
        }

        private static void DrawHeroBonusPanel(Graphics g, GameEngine engine)
        {
            PlayerState player = engine.CurrentRun.Player;
            CharacterDefinition hero = engine.GetActiveCharacterDefinition();
            Rectangle heroPanel = new(20, 66, 384, 176);
            UiHelpers.DrawRoundedBox(g, heroPanel, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using Font headerFont = new("Segoe UI", 10.5f, FontStyle.Bold);
            using Font boldFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font bodyFont = new("Segoe UI", 8.8f, FontStyle.Regular);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush cyanBrush = new(UiTheme.Cyan);
            using SolidBrush primaryBrush = new(UiTheme.TextPrimary);
            using SolidBrush secondaryBrush = new(UiTheme.TextSecondary);

            g.DrawString("HERO & ACTIVE BONUSES", headerFont, goldBrush, heroPanel.X + 14, heroPanel.Y + 11);
            g.DrawString($"Position: ({player.Position.X}, {player.Position.Y})   |   Free Rerolls: {player.FreeRerollsRemaining}", boldFont, cyanBrush, heroPanel.X + 14, heroPanel.Y + 36);

            string specialStatus = player.SpecialCooldown == 0 ? "READY" : $"Cooldown: {player.SpecialCooldown} / {player.SpecialMaxCooldown}";
            g.DrawString($"Special: {hero.SpecialName} ({specialStatus})", boldFont, player.SpecialCooldown == 0 ? goldBrush : secondaryBrush, heroPanel.X + 14, heroPanel.Y + 58);

            string specialDesc = engine.GetSpecialDamageBonus() > 0
                ? $"{hero.SpecialDescription} (+{engine.GetSpecialDamageBonus()} dmg)"
                : hero.SpecialDescription;
            g.DrawString(specialDesc, bodyFont, secondaryBrush, new Rectangle(heroPanel.X + 14, heroPanel.Y + 78, heroPanel.Width - 28, 38));

            List<string> bonuses = BuildUpgradeSummaryLines(engine);
            string bonusText = bonuses.Count > 0 ? $"Bonuses: {string.Join(" | ", bonuses)}" : "Bonuses: None unlocked yet (buy in Main Menu)";
            Rectangle bonusRect = new(heroPanel.X + 14, heroPanel.Y + 122, heroPanel.Width - 28, 44);
            UiHelpers.DrawRoundedBox(g, bonusRect, 6, Color.FromArgb(22, 27, 37), Color.FromArgb(46, 56, 73), 1f);
            g.DrawString(bonusText, bodyFont, bonuses.Count > 0 ? primaryBrush : secondaryBrush, new Rectangle(bonusRect.X + 8, bonusRect.Y + 6, bonusRect.Width - 16, bonusRect.Height - 12));
        }

        private static void DrawLegendPanel(Graphics g)
        {
            Rectangle legendPanel = new(20, 254, 384, 134);
            UiHelpers.DrawRoundedBox(g, legendPanel, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using Font headerFont = new("Segoe UI", 10.5f, FontStyle.Bold);
            using Font itemFont = new("Segoe UI", 8.8f, FontStyle.Regular);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush textBrush = new(UiTheme.TextPrimary);

            g.DrawString("TACTICAL LEGEND", headerFont, goldBrush, legendPanel.X + 14, legendPanel.Y + 11);
            DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 38, UiTheme.Cyan, "Cyan/Yellow Tile: Move / Movement Aim Preview", itemFont, textBrush);
            DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 62, UiTheme.Red, "Pulsing Red Tile: Enemy Charged Attack Telegraph", itemFont, textBrush);
            DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 86, UiTheme.Green, "Green Bubbling Pool: Poison Ground Hazard (3T)", itemFont, textBrush);
            DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 110, UiTheme.Gold, "Spiked Iron Jaws: Bear Trap (2 Dmg + 1T Stun)", itemFont, textBrush);
        }

        private static void DrawControlsPanel(Graphics g)
        {
            Rectangle controlsPanel = new(20, 400, 384, 98);
            UiHelpers.DrawRoundedBox(g, controlsPanel, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using Font headerFont = new("Segoe UI", 10f, FontStyle.Bold);
            using Font ctrlFont = new("Segoe UI", 8.7f, FontStyle.Regular);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush mutedBrush = new(UiTheme.TextSecondary);

            g.DrawString("CONTROLS", headerFont, goldBrush, controlsPanel.X + 14, controlsPanel.Y + 9);
            g.DrawString("Move: WASD / Arrows / Click Adjacent  |  Wait: Space  |  Special: E", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 33);
            g.DrawString("Equip Card: Click Ready Hand Card (1T)  |  Reorder: Drag Queue (0T)", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 53);
            g.DrawString("Play Queue: Enter / Button  |  Aim: Hover & Click (or Keys + Enter)", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 73);
        }

        private void DrawArenaGrid(
            Graphics g,
            GameEngine engine,
            Point mouseLocation,
            float specialAnimProgress,
            float telegraphPulsePhase,
            AimPreviewResult aimPreview)
        {
            UiHelpers.DrawGradientRoundedBox(
                g,
                _arenaFrameRect,
                12,
                Color.FromArgb(24, 30, 42),
                Color.FromArgb(16, 20, 29),
                engine.IsExecutingQueue ? UiTheme.Orange : UiTheme.Gold,
                2.2f);

            (int startX, int startY) = GetGridOrigin();
            DrawGridTiles(g, engine, startX, startY, mouseLocation);
            DrawHazardsOnGrid(g, engine.CurrentRun.Hazards, startX, startY, telegraphPulsePhase);
            DrawEnemyTelegraphs(g, engine.CurrentRun.Enemies, startX, startY, telegraphPulsePhase);

            if (engine.IsExecutingQueue && aimPreview is { IsValidAim: true })
            {
                DrawAimPreviewOverlay(g, startX, startY, aimPreview);
            }

            if (specialAnimProgress > 0f && !string.IsNullOrEmpty(engine.LastSpecialEffectType))
            {
                DrawSpecialAbilityEffect(g, startX, startY, engine.LastSpecialOrigin, engine.LastSpecialEffectType, specialAnimProgress);
            }

            DrawUnitsOnGrid(g, engine, startX, startY, telegraphPulsePhase);
            DrawAimingBannerIfActive(g, engine);
        }

        private void DrawGridTiles(Graphics g, GameEngine engine, int startX, int startY, Point mouseLocation)
        {
            HashSet<GridPos> validMoveSet = [.. engine.GetValidMoveTiles()];
            GridPos playerPos = engine.CurrentRun.Player.Position;

            for (int row = 0; row < GridSize; row++)
            {
                for (int col = 0; col < GridSize; col++)
                {
                    GridPos pos = new(col, row);
                    Rectangle tileRect = GetTileRect(startX, startY, col, row);
                    bool isCheckerAlt = ((row + col) % 2) == 1;
                    bool isValidMove = !engine.IsExecutingQueue && validMoveSet.Contains(pos);
                    bool isHovered = tileRect.Contains(mouseLocation);

                    Color tileTop = isValidMove
                        ? (isHovered ? Color.FromArgb(50, 92, 116) : Color.FromArgb(38, 62, 80))
                        : (isCheckerAlt ? Color.FromArgb(39, 48, 63) : Color.FromArgb(33, 41, 54));
                    Color tileBot = isValidMove
                        ? (isHovered ? Color.FromArgb(34, 66, 86) : Color.FromArgb(28, 46, 62))
                        : (isCheckerAlt ? Color.FromArgb(31, 38, 51) : Color.FromArgb(26, 33, 45));
                    Color tileBorder = isValidMove
                        ? (isHovered ? Color.FromArgb(132, 232, 250) : Color.FromArgb(66, 146, 172))
                        : Color.FromArgb(52, 64, 82);
                    float borderWidth = isValidMove ? (isHovered ? 2.2f : 1.5f) : 1.2f;

                    UiHelpers.DrawGradientRoundedBox(g, tileRect, 7, tileTop, tileBot, tileBorder, borderWidth);

                    if (isValidMove)
                    {
                        DrawMoveDirectionHint(g, tileRect, playerPos, pos, isHovered);
                        AddHitbox(tileRect, ArenaActionType.MoveToTile, tilePos: pos, isEnabled: true);
                    }
                    else if (engine.IsExecutingQueue && pos != playerPos)
                    {
                        AddHitbox(tileRect, ArenaActionType.AimTile, tilePos: pos, isEnabled: true);
                    }
                }
            }
        }

        private static void DrawUnitsOnGrid(Graphics g, GameEngine engine, int startX, int startY, float telegraphPulsePhase)
        {
            foreach (EnemyInstance enemy in engine.CurrentRun.Enemies)
            {
                if (enemy.CurrentHp > 0 && enemy.Position.IsValid())
                {
                    DrawEnemySpriteOnTile(g, GetTileRect(startX, startY, enemy.Position.X, enemy.Position.Y), enemy, telegraphPulsePhase);
                }
            }

            PlayerState player = engine.CurrentRun.Player;
            DrawPlayerSpriteOnTile(g, GetTileRect(startX, startY, player.Position.X, player.Position.Y), player, engine.GetActiveCharacterDefinition());
        }

        private void DrawAimingBannerIfActive(Graphics g, GameEngine engine)
        {
            if (!engine.IsExecutingQueue || engine.CurrentAimingCard == null)
            {
                return;
            }

            CardInstance activeCard = engine.CurrentAimingCard;
            Rectangle bannerRect = new(_arenaFrameRect.X + 16, _arenaFrameRect.Y + 6, _arenaFrameRect.Width - 32, 26);
            UiHelpers.DrawRoundedBox(g, bannerRect, 6, Color.FromArgb(228, 44, 24, 14), UiTheme.Orange, 1.5f);

            using Font bannerFont = new("Segoe UI", 8.8f, FontStyle.Bold);
            using SolidBrush whiteBrush = new(Color.White);
            using StringFormat sf = UiHelpers.CreateStringFormat();
            string bannerText = $"AIMING [{engine.CurrentExecutingQueueIndex + 1}/{engine.CurrentRun.Player.EquippedQueue.Count}]: {activeCard.Definition.Name} ({activeCard.Definition.Targeting}) — Click to Confirm";
            g.DrawString(bannerText, bannerFont, whiteBrush, bannerRect, sf);
        }

        private static void DrawEnemyTelegraphs(
            Graphics g,
            List<EnemyInstance> enemies,
            int startX,
            int startY,
            float pulsePhase)
        {
            double pulse = 0.5 + (0.5 * Math.Sin(pulsePhase));
            int fillAlpha = 72 + (int)(pulse * 64);
            int borderAlpha = 170 + (int)(pulse * 85);

            foreach (EnemyInstance enemy in enemies.Where(e => e.CurrentHp > 0 && e.State == EnemyState.Charging && e.LockedTargetTiles.Count > 0))
            {
                foreach (GridPos target in enemy.LockedTargetTiles.Where(t => t.IsValid()))
                {
                    Rectangle tileRect = GetTileRect(startX, startY, target.X, target.Y);
                    UiHelpers.DrawRoundedBox(g, tileRect, 7, Color.FromArgb(fillAlpha, 224, 48, 48), Color.FromArgb(borderAlpha, 255, 96, 96), 2.3f);
                    DrawTelegraphMarkers(g, tileRect, enemy.LockedDirection, borderAlpha);
                }
            }
        }

        private static void DrawTelegraphMarkers(Graphics g, Rectangle tileRect, GridPos lockedDirection, int borderAlpha)
        {
            using (Pen warnPen = new(Color.FromArgb(borderAlpha, 255, 204, 128), 1.8f))
            {
                g.DrawLine(warnPen, tileRect.X + 8, tileRect.Y + 8, tileRect.X + 18, tileRect.Y + 8);
                g.DrawLine(warnPen, tileRect.X + 8, tileRect.Y + 8, tileRect.X + 8, tileRect.Y + 18);
                g.DrawLine(warnPen, tileRect.Right - 8, tileRect.Bottom - 8, tileRect.Right - 18, tileRect.Bottom - 8);
                g.DrawLine(warnPen, tileRect.Right - 8, tileRect.Bottom - 8, tileRect.Right - 8, tileRect.Bottom - 18);
            }

            if (lockedDirection.X != 0 || lockedDirection.Y != 0)
            {
                DrawDirectionalChevron(g, tileRect, lockedDirection, Color.FromArgb(borderAlpha, 255, 196, 132));
            }

            using Font warnFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using SolidBrush warnBrush = new(Color.FromArgb(borderAlpha, 255, 232, 184));
            g.DrawString("!", warnFont, warnBrush, tileRect.Right - 15, tileRect.Y + 2);
        }

        private static void DrawDirectionalChevron(Graphics g, Rectangle tileRect, GridPos dir, Color color)
        {
            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2);
            const int s = 8;

            Point[] chevron = dir switch
            {
                { X: > 0 } => [new(cx - 4, cy - s), new(cx + 6, cy), new(cx - 4, cy + s)],
                { X: < 0 } => [new(cx + 4, cy - s), new(cx - 6, cy), new(cx + 4, cy + s)],
                { Y: < 0 } => [new(cx - s, cy + 4), new(cx, cy - 6), new(cx + s, cy + 4)],
                _ => [new(cx - s, cy - 4), new(cx, cy + 6), new(cx + s, cy - 4)]
            };

            using Pen pen = new(color, 2.2f);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, chevron);
        }

        private static void DrawEnemySpriteOnTile(Graphics g, Rectangle tileRect, EnemyInstance enemy, float pulsePhase)
        {
            bool isCharging = enemy.State == EnemyState.Charging;
            bool isBoss = enemy.IsBoss;
            double pulse = 0.5 + (0.5 * Math.Sin(pulsePhase * 1.4));

            Color frameBorder = isCharging
                ? Color.FromArgb(180 + (int)(pulse * 75), 255, 86, 86)
                : (isBoss ? UiTheme.Gold : Color.FromArgb(156, 76, 76));

            UiHelpers.DrawRoundedBox(
                g,
                new Rectangle(tileRect.X + 2, tileRect.Y + 2, tileRect.Width - 4, tileRect.Height - 4),
                6,
                isBoss ? Color.FromArgb(62, 134, 48, 38) : Color.FromArgb(46, 118, 48, 52),
                frameBorder,
                isCharging || isBoss ? 2.2f : 1.5f);

            DrawEnemyShape(g, enemy.Type, tileRect.X + (tileRect.Width / 2), tileRect.Y + (tileRect.Height / 2) + 2);
            DrawEnemyLabelsAndBadges(g, tileRect, enemy, isBoss);
            DrawUnitHpBar(g, new Rectangle(tileRect.X + 6, tileRect.Bottom - 10, tileRect.Width - 12, 7), enemy.CurrentHp, enemy.MaxHp, isBoss ? UiTheme.Gold : UiTheme.Red);
        }

        private static void DrawEnemyShape(Graphics g, EnemyType type, int cx, int cy)
        {
            switch (type)
            {
                case EnemyType.Pawn:
                    EntityGraphics.DrawPawn(g, cx, cy, 1.0f);
                    break;
                case EnemyType.Archer:
                    EntityGraphics.DrawArcher(g, cx, cy, 1.0f);
                    break;
                case EnemyType.Spearman:
                    EntityGraphics.DrawSpearman(g, cx, cy, 1.0f);
                    break;
                case EnemyType.Shaman:
                    EntityGraphics.DrawShaman(g, cx, cy, 1.0f);
                    break;
                case EnemyType.VikingChief:
                    EntityGraphics.DrawVikingChief(g, cx, cy, 1.0f);
                    break;
            }
        }

        private static void DrawEnemyLabelsAndBadges(Graphics g, Rectangle tileRect, EnemyInstance enemy, bool isBoss)
        {
            (string intentText, Color intentBg, Color intentFg) = GetEnemyIntentDisplay(enemy);

            using (Font badgeFont = new("Segoe UI", 6.3f, FontStyle.Bold))
            using (Font nameFont = new("Segoe UI", 6.7f, FontStyle.Bold))
            using (SolidBrush intentFgBrush = new(intentFg))
            using (SolidBrush nameBrush = new(UiTheme.TextPrimary))
            using (SolidBrush labelBg = new(Color.FromArgb(185, 14, 18, 25)))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                Rectangle intentRect = new(tileRect.X + 4, tileRect.Y + 2, tileRect.Width - 8, 12);
                UiHelpers.DrawRoundedBox(g, intentRect, 3, intentBg, intentBg, 0f);
                g.DrawString(intentText, badgeFont, intentFgBrush, intentRect, sf);

                Rectangle nameRect = new(tileRect.X + 3, tileRect.Bottom - 22, tileRect.Width - 6, 11);
                g.FillRectangle(labelBg, nameRect);
                g.DrawString(isBoss ? $"{enemy.Name} ({enemy.CurrentHp})" : enemy.Name, nameFont, nameBrush, nameRect, sf);
            }

            if (enemy.PoisonTurnsRemaining > 0)
            {
                Rectangle poisonDot = new(tileRect.X + 4, tileRect.Y + 16, 22, 13);
                UiHelpers.DrawRoundedBox(g, poisonDot, 3, Color.FromArgb(38, 124, 64), Color.FromArgb(124, 240, 152), 1f);
                EntityGraphics.DrawPoisonVialIcon(g, poisonDot.X + 6, poisonDot.Y + 6, 9);

                using Font pFont = new("Segoe UI", 6.1f, FontStyle.Bold);
                using SolidBrush whiteBrush = new(Color.White);
                using StringFormat sf = UiHelpers.CreateStringFormat();
                g.DrawString($"{enemy.PoisonTurnsRemaining}", pFont, whiteBrush, poisonDot with { X = poisonDot.X + 10, Width = 12 }, sf);
            }
        }

        private static (string Text, Color Bg, Color Fg) GetEnemyIntentDisplay(EnemyInstance enemy)
        {
            if (enemy.IsStunned)
            {
                return ("STUNNED", Color.FromArgb(156, 114, 28), Color.White);
            }

            if (enemy.State == EnemyState.Unarmed)
            {
                return ("EQUIPPING", Color.FromArgb(62, 70, 86), UiTheme.TextSecondary);
            }

            if (enemy.State == EnemyState.Charging)
            {
                string label = enemy.ChargeTurnsRemaining > 1 ? $"CHARGE {enemy.ChargeTurnsRemaining}T!" : "STRIKE!";
                return (label, Color.FromArgb(196, 42, 42), Color.White);
            }

            return (enemy.EquippedWeaponLabel, Color.FromArgb(42, 50, 66), UiTheme.Gold);
        }

        private static void DrawHazardsOnGrid(Graphics g, List<GroundHazard> hazards, int startX, int startY, float pulsePhase)
        {
            foreach (GroundHazard hazard in hazards.Where(h => h.Position.IsValid()))
            {
                Rectangle tileRect = GetTileRect(startX, startY, hazard.Position.X, hazard.Position.Y);
                if (hazard.Type == HazardType.BearTrap)
                {
                    EntityGraphics.DrawBearTrapHazard(g, tileRect, hazard.IsArmed);
                }
                else if (hazard.Type == HazardType.Poison)
                {
                    EntityGraphics.DrawPoisonPoolHazard(g, tileRect, hazard.RemainingTurns, pulsePhase);
                }
            }
        }

        private static void DrawAimPreviewOverlay(Graphics g, int startX, int startY, AimPreviewResult preview)
        {
            HashSet<GridPos> hitTiles = [.. preview.HitEnemyTiles];

            foreach (GridPos pos in preview.AffectedTiles)
            {
                Rectangle tileRect = GetTileRect(startX, startY, pos.X, pos.Y);
                int cx = tileRect.X + (tileRect.Width / 2);
                int cy = tileRect.Y + (tileRect.Height / 2);

                if (preview.IsMovement)
                {
                    bool isDest = preview.MoveDestination == pos;
                    UiHelpers.DrawRoundedBox(
                        g,
                        tileRect,
                        7,
                        isDest ? Color.FromArgb(145, 244, 212, 72) : Color.FromArgb(95, 72, 189, 214),
                        isDest ? Color.FromArgb(255, 250, 228, 120) : UiTheme.Cyan,
                        isDest ? 2.8f : 1.8f);

                    if (isDest)
                    {
                        using Pen destPen = new(Color.White, 2.2f);
                        g.DrawEllipse(destPen, cx - 14, cy - 14, 28, 28);
                    }
                }
                else
                {
                    bool hitsEnemy = hitTiles.Contains(pos);
                    UiHelpers.DrawRoundedBox(
                        g,
                        tileRect,
                        7,
                        hitsEnemy ? Color.FromArgb(155, 226, 56, 56) : Color.FromArgb(110, 238, 136, 46),
                        hitsEnemy ? Color.FromArgb(255, 255, 132, 122) : UiTheme.Orange,
                        hitsEnemy ? 2.8f : 2f);

                    if (hitsEnemy)
                    {
                        using Pen crossPen = new(Color.White, 2.2f);
                        g.DrawEllipse(crossPen, cx - 16, cy - 16, 32, 32);
                        g.DrawLine(crossPen, cx - 22, cy, cx + 22, cy);
                        g.DrawLine(crossPen, cx, cy - 22, cx, cy + 22);
                    }
                }
            }
        }

        private static void DrawMoveDirectionHint(Graphics g, Rectangle tileRect, GridPos playerPos, GridPos targetPos, bool isHovered)
        {
            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2);
            int dx = targetPos.X - playerPos.X;
            int dy = targetPos.Y - playerPos.Y;
            int s = isHovered ? 9 : 7;

            Point[] triangle = (dx, dy) switch
            {
                (1, _) => [new(cx + s, cy), new(cx - s + 2, cy - s + 1), new(cx - s + 2, cy + s - 1)],
                (-1, _) => [new(cx - s, cy), new(cx + s - 2, cy - s + 1), new(cx + s - 2, cy + s - 1)],
                (_, -1) => [new(cx, cy - s), new(cx - s + 1, cy + s - 2), new(cx + s - 1, cy + s - 2)],
                _ => [new(cx, cy + s), new(cx - s + 1, cy - s + 2), new(cx + s - 1, cy - s + 2)]
            };

            using SolidBrush brush = new(isHovered ? Color.FromArgb(228, 136, 232, 248) : Color.FromArgb(100, 72, 189, 214));
            g.FillPolygon(brush, triangle);
        }

        private static void DrawSpecialAbilityEffect(
            Graphics g,
            int gridStartX,
            int gridStartY,
            GridPos origin,
            string effectType,
            float progress)
        {
            Rectangle originTile = GetTileRect(gridStartX, gridStartY, origin.X, origin.Y);
            int cx = originTile.X + (originTile.Width / 2);
            int cy = originTile.Y + (originTile.Height / 2);
            int alpha = Math.Clamp((int)(progress * 210), 0, 210);

            if (effectType == "VikingScream")
            {
                using SolidBrush pulseBrush = new(Color.FromArgb(alpha / 2, 236, 184, 62));
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        GridPos adj = origin.Offset(dx, dy);
                        if ((dx != 0 || dy != 0) && adj.IsValid())
                        {
                            g.FillRectangle(pulseBrush, GetTileRect(gridStartX, gridStartY, adj.X, adj.Y));
                        }
                    }
                }

                int radius = 26 + (int)((1f - progress) * 96);
                using Pen ringPen = new(Color.FromArgb(alpha, 245, 200, 86), 4f);
                g.DrawEllipse(ringPen, cx - radius, cy - radius, radius * 2, radius * 2);
            }
            else if (effectType == "IronBulwark")
            {
                int radius = 34 + (int)((1f - progress) * 18);
                using SolidBrush domeBrush = new(Color.FromArgb(alpha / 2, 72, 208, 235));
                using Pen domePen = new(Color.FromArgb(alpha, 152, 242, 255), 3.5f);
                g.FillEllipse(domeBrush, cx - radius, cy - radius, radius * 2, radius * 2);
                g.DrawEllipse(domePen, cx - radius, cy - radius, radius * 2, radius * 2);
            }
        }

        private static void DrawPlayerSpriteOnTile(Graphics g, Rectangle tileRect, PlayerState player, CharacterDefinition hero)
        {
            UiHelpers.DrawRoundedBox(
                g,
                new Rectangle(tileRect.X + 2, tileRect.Y + 2, tileRect.Width - 4, tileRect.Height - 4),
                6,
                Color.FromArgb(52, 58, 96, 142),
                hero.Id == "shieldmaiden" ? UiTheme.Cyan : UiTheme.Gold,
                2f);

            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2) - 1;

            if (string.Equals(hero.Id, "shieldmaiden", StringComparison.OrdinalIgnoreCase))
            {
                EntityGraphics.DrawShieldmaiden(g, cx, cy, 1.0f, false);
            }
            else
            {
                EntityGraphics.DrawViking(g, cx, cy, 1.0f);
            }

            using (Font nameFont = new("Segoe UI", 7.2f, FontStyle.Bold))
            using (SolidBrush labelBg = new(Color.FromArgb(190, 14, 18, 26)))
            using (SolidBrush whiteBrush = new(UiTheme.TextPrimary))
            using (StringFormat sf = UiHelpers.CreateStringFormat())
            {
                Rectangle labelRect = new(tileRect.X + 4, tileRect.Y + 3, tileRect.Width - 8, 14);
                g.FillRectangle(labelBg, labelRect);
                g.DrawString(hero.Name, nameFont, whiteBrush, labelRect, sf);
            }

            DrawUnitHpBar(g, new Rectangle(tileRect.X + 8, tileRect.Bottom - 11, tileRect.Width - 16, 7), player.CurrentHp, player.MaxHp, Color.FromArgb(72, 204, 122));
        }

        private static void DrawUnitHpBar(Graphics g, Rectangle hpBg, int currentHp, int maxHp, Color fillColor)
        {
            using SolidBrush bgBrush = new(Color.FromArgb(35, 18, 22));
            using Pen borderPen = new(Color.FromArgb(16, 20, 28), 1f);
            g.FillRectangle(bgBrush, hpBg);

            float ratio = maxHp > 0 ? Math.Clamp((float)currentHp / maxHp, 0f, 1f) : 0f;
            int fillW = (int)(hpBg.Width * ratio);
            if (fillW > 0)
            {
                using SolidBrush fillBrush = new(fillColor);
                g.FillRectangle(fillBrush, hpBg.X, hpBg.Y, fillW, hpBg.Height);
            }

            g.DrawRectangle(borderPen, hpBg);
        }

        private static void DrawRightCombatLogPanel(Graphics g, GameEngine engine)
        {
            Rectangle logPanel = new(876, 66, 384, 432);
            UiHelpers.DrawRoundedBox(g, logPanel, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using Font headerFont = new("Segoe UI", 10.5f, FontStyle.Bold);
            using Font subFont = new("Segoe UI", 8.8f, FontStyle.Bold);
            using Font logFont = new("Segoe UI", 8.5f, FontStyle.Regular);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush cyanBrush = new(UiTheme.Cyan);
            using SolidBrush primaryBrush = new(UiTheme.TextPrimary);
            using SolidBrush secondaryBrush = new(UiTheme.TextSecondary);
            using StringFormat sf = UiHelpers.CreateStringFormat();

            g.DrawString("COMBAT LOG (LATEST 8 ENTRIES)", headerFont, goldBrush, logPanel.X + 14, logPanel.Y + 12);

            Rectangle waveSummary = new(logPanel.X + 14, logPanel.Y + 38, logPanel.Width - 28, 28);
            UiHelpers.DrawRoundedBox(g, waveSummary, 6, Color.FromArgb(22, 28, 38), Color.FromArgb(46, 56, 73), 1f);
            int aliveEnemies = engine.CurrentRun.Enemies.Count(e => e.CurrentHp > 0);
            g.DrawString($"Wave {engine.CurrentRun.CurrentWave}  |  Enemies: {aliveEnemies}  |  Turn: {engine.CurrentRun.TurnNumber}", subFont, cyanBrush, waveSummary, sf);

            List<string> entries = engine.CurrentRun.RecentLogEntries;
            int startIndex = Math.Max(0, entries.Count - 8);
            int rowY = logPanel.Y + 74;

            for (int i = startIndex; i < entries.Count; i++)
            {
                bool isLatest = i == entries.Count - 1;
                Rectangle rowRect = new(logPanel.X + 14, rowY, logPanel.Width - 28, 38);
                UiHelpers.DrawRoundedBox(g, rowRect, 6, isLatest ? Color.FromArgb(38, 48, 64) : Color.FromArgb(24, 30, 41), isLatest ? UiTheme.Gold : Color.FromArgb(42, 51, 67), 1f);
                g.DrawString(entries[i], logFont, isLatest ? primaryBrush : secondaryBrush, new Rectangle(rowRect.X + 8, rowRect.Y + 3, rowRect.Width - 16, rowRect.Height - 6));
                rowY += 42;
            }

            if (!string.IsNullOrWhiteSpace(engine.LastActionFeedback))
            {
                g.DrawString(engine.LastActionFeedback, subFont, goldBrush, new Rectangle(logPanel.X + 14, logPanel.Bottom - 28, logPanel.Width - 28, 22));
            }
        }

        private void DrawBottomDock(Graphics g, GameEngine engine, Point mouseLocation, int dragSourceIndex, int dragHoverTargetIndex)
        {
            DrawHandPanel(g, engine, mouseLocation);
            DrawEquippedQueuePanel(g, engine, dragSourceIndex, dragHoverTargetIndex);
            DrawTurnActionsPanel(g, engine, mouseLocation);
        }

        private void DrawHandPanel(Graphics g, GameEngine engine, Point mouseLocation)
        {
            PlayerState player = engine.CurrentRun.Player;
            Rectangle handPanel = new(20, 506, 616, 204);
            UiHelpers.DrawRoundedBox(g, handPanel, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using (Font headerFont = new("Segoe UI", 9.8f, FontStyle.Bold))
            using (Font hintFont = new("Segoe UI", 8.2f, FontStyle.Regular))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            using (SolidBrush mutedBrush = new(UiTheme.TextSecondary))
            {
                g.DrawString($"HAND ({player.Hand.Count} CARDS)", headerFont, goldBrush, handPanel.X + 12, handPanel.Y + 8);
                g.DrawString("Click Ready Card to Equip (1 Turn)", hintFont, mutedBrush, handPanel.X + 170, handPanel.Y + 10);
            }

            for (int i = 0; i < player.Hand.Count && i < 4; i++)
            {
                CardInstance card = player.Hand[i];
                Rectangle cardRect = new(handPanel.X + 10 + (i * 152), handPanel.Y + 32, 144, 164);
                bool canEquip = !engine.IsExecutingQueue && card.IsReady && player.EquippedQueue.Count < GameEngine.MaxEquippedSlots;
                DrawHandCard(g, engine, card, cardRect, canEquip, cardRect.Contains(mouseLocation) && canEquip);
                AddHitbox(cardRect, ArenaActionType.EquipHandCard, index: i, isEnabled: canEquip);
            }
        }

        private void DrawEquippedQueuePanel(Graphics g, GameEngine engine, int dragSourceIndex, int dragHoverTargetIndex)
        {
            PlayerState player = engine.CurrentRun.Player;
            Rectangle queuePanel = new(646, 506, 368, 204);
            UiHelpers.DrawRoundedBox(g, queuePanel, 10, UiTheme.Panel, engine.IsExecutingQueue ? UiTheme.Orange : UiTheme.Border, engine.IsExecutingQueue ? 2f : 1.5f);

            using Font headerFont = new("Segoe UI", 9.8f, FontStyle.Bold);
            using Font slotFont = new("Segoe UI", 8.4f, FontStyle.Bold);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush mutedBrush = new(UiTheme.Muted);
            using StringFormat sf = UiHelpers.CreateStringFormat();

            string queueTitle = engine.IsExecutingQueue
                ? $"EXECUTING QUEUE ({engine.CurrentExecutingQueueIndex + 1}/{player.EquippedQueue.Count})"
                : $"EQUIPPED ({player.EquippedQueue.Count}/{GameEngine.MaxEquippedSlots}) — DRAG TO REORDER";
            g.DrawString(queueTitle, headerFont, goldBrush, queuePanel.X + 10, queuePanel.Y + 8);

            for (int i = 0; i < GameEngine.MaxEquippedSlots; i++)
            {
                Rectangle slotRect = new(queuePanel.X + 8 + (i * 120), queuePanel.Y + 32, 112, 164);
                _equippedSlotRects.Add(slotRect);

                if (i >= player.EquippedQueue.Count)
                {
                    UiHelpers.DrawRoundedBox(g, slotRect, 8, Color.FromArgb(22, 27, 36), Color.FromArgb(48, 58, 74), 1.2f);
                    g.DrawString($"SLOT #{i + 1}\nEmpty", slotFont, mutedBrush, slotRect, sf);
                    continue;
                }

                bool isExecutingThis = engine.IsExecutingQueue && engine.CurrentExecutingQueueIndex == i;
                bool isBeingDragged = dragSourceIndex == i;
                DrawEquippedCard(g, engine, player.EquippedQueue[i], slotRect, i, isExecutingThis, false, isBeingDragged);

                if (dragSourceIndex >= 0 && dragHoverTargetIndex == i && !isBeingDragged)
                {
                    using Pen dropPen = new(UiTheme.Gold, 2.5f);
                    dropPen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(dropPen, slotRect.X + 2, slotRect.Y + 2, slotRect.Width - 4, slotRect.Height - 4);
                }

                AddHitbox(slotRect, ArenaActionType.EquippedSlot, index: i, isEnabled: !engine.IsExecutingQueue);
            }
        }

        private void DrawTurnActionsPanel(Graphics g, GameEngine engine, Point mouseLocation)
        {
            Rectangle actionPanel = new(1024, 506, 236, 204);
            UiHelpers.DrawRoundedBox(g, actionPanel, 10, UiTheme.Panel, UiTheme.Border, 1.5f);

            using (Font headerFont = new("Segoe UI", 9.8f, FontStyle.Bold))
            using (SolidBrush goldBrush = new(UiTheme.Gold))
            {
                g.DrawString("TURN ACTIONS", headerFont, goldBrush, actionPanel.X + 12, actionPanel.Y + 8);
            }

            DrawPlayQueueButton(g, engine, new Rectangle(actionPanel.X + 12, actionPanel.Y + 34, actionPanel.Width - 24, 48), mouseLocation);
            DrawSpecialActionButton(g, engine, new Rectangle(actionPanel.X + 12, actionPanel.Y + 90, actionPanel.Width - 24, 52), mouseLocation);
            DrawWaitActionButton(g, engine, new Rectangle(actionPanel.X + 12, actionPanel.Y + 150, actionPanel.Width - 24, 42), mouseLocation);
        }

        private void DrawPlayQueueButton(Graphics g, GameEngine engine, Rectangle playBtn, Point mouseLocation)
        {
            PlayerState player = engine.CurrentRun.Player;
            if (engine.IsExecutingQueue)
            {
                bool canCancel = engine.CurrentExecutingQueueIndex == 0;
                bool hoverCancel = playBtn.Contains(mouseLocation) && canCancel;
                UiHelpers.DrawRoundedBox(
                    g,
                    playBtn,
                    8,
                    canCancel ? (hoverCancel ? Color.FromArgb(148, 54, 54) : Color.FromArgb(114, 42, 42)) : Color.FromArgb(44, 34, 24),
                    canCancel ? UiTheme.Red : UiTheme.Orange,
                    1.5f);

                using Font btnFont = new("Segoe UI", 9.2f, FontStyle.Bold);
                using SolidBrush whiteBrush = new(Color.White);
                using StringFormat sf = UiHelpers.CreateStringFormat();
                g.DrawString(canCancel ? "CANCEL AIMING [ESC]" : "AIM & CLICK TILE", btnFont, whiteBrush, playBtn, sf);
                AddHitbox(playBtn, ArenaActionType.CancelAim, isEnabled: canCancel);
                return;
            }

            if (player.EquippedQueue.Count > 0)
            {
                bool hoverPlay = playBtn.Contains(mouseLocation);
                UiHelpers.DrawGradientButton(
                    g,
                    playBtn,
                    hoverPlay ? Color.FromArgb(88, 202, 134) : Color.FromArgb(62, 166, 107),
                    hoverPlay ? Color.FromArgb(56, 158, 98) : Color.FromArgb(44, 128, 80),
                    Color.FromArgb(124, 228, 162),
                    $"PLAY QUEUE ({player.EquippedQueue.Count}) [Enter]",
                    9.5f,
                    Color.White);
                AddHitbox(playBtn, ArenaActionType.PlayQueue, isEnabled: true);
                return;
            }

            UiHelpers.DrawDisabledButton(g, playBtn, 8, "PLAY QUEUE (EMPTY)", 9.2f);
        }

        private void DrawSpecialActionButton(Graphics g, GameEngine engine, Rectangle specialBtn, Point mouseLocation)
        {
            PlayerState player = engine.CurrentRun.Player;
            CharacterDefinition hero = engine.GetActiveCharacterDefinition();
            bool specialReady = !engine.IsExecutingQueue && player.SpecialCooldown <= 0;
            bool hoverSpecial = specialBtn.Contains(mouseLocation) && specialReady;

            if (specialReady)
            {
                bool isShieldmaiden = hero.Id == "shieldmaiden";
                Color spTop = isShieldmaiden
                    ? (hoverSpecial ? Color.FromArgb(88, 206, 232) : Color.FromArgb(58, 166, 194))
                    : (hoverSpecial ? Color.FromArgb(238, 184, 72) : Color.FromArgb(206, 150, 42));
                Color spBottom = isShieldmaiden
                    ? (hoverSpecial ? Color.FromArgb(52, 154, 182) : Color.FromArgb(38, 124, 150))
                    : (hoverSpecial ? Color.FromArgb(192, 136, 30) : Color.FromArgb(162, 112, 24));

                UiHelpers.DrawGradientButton(
                    g,
                    specialBtn,
                    spTop,
                    spBottom,
                    isShieldmaiden ? UiTheme.Cyan : UiTheme.Gold,
                    $"{hero.SpecialName.ToUpperInvariant()} [E]\nREADY (1 Turn)",
                    8.8f,
                    isShieldmaiden ? Color.White : UiTheme.DarkButtonText);
            }
            else
            {
                string cdLabel = engine.IsExecutingQueue ? "AIMING IN PROGRESS" : $"COOLDOWN: {player.SpecialCooldown} TURNS";
                UiHelpers.DrawDisabledButton(g, specialBtn, 8, $"{hero.SpecialName.ToUpperInvariant()} [E]\n{cdLabel}", 8.8f);
            }

            AddHitbox(specialBtn, ArenaActionType.UseSpecial, isEnabled: specialReady);
        }

        private void DrawWaitActionButton(Graphics g, GameEngine engine, Rectangle waitBtn, Point mouseLocation)
        {
            bool canWait = !engine.IsExecutingQueue;
            bool hoverWait = waitBtn.Contains(mouseLocation) && canWait;
            Color waitTop = canWait ? (hoverWait ? Color.FromArgb(66, 86, 114) : Color.FromArgb(46, 60, 82)) : Color.FromArgb(30, 35, 45);
            Color waitBottom = canWait ? (hoverWait ? Color.FromArgb(48, 64, 88) : Color.FromArgb(34, 44, 62)) : Color.FromArgb(26, 31, 40);
            string waitLabel = engine.GetWaitEquippedCooldownBonus() > 0 ? "WAIT [Space] (+1 CD)" : "WAIT TURN [Space]";

            UiHelpers.DrawGradientButton(g, waitBtn, waitTop, waitBottom, hoverWait ? UiTheme.Cyan : UiTheme.Border, waitLabel, 9.2f, canWait ? UiTheme.TextPrimary : UiTheme.Muted);
            AddHitbox(waitBtn, ArenaActionType.WaitTurn, isEnabled: canWait);
        }

        private static void DrawHandCard(Graphics g, GameEngine engine, CardInstance card, Rectangle rect, bool canEquip, bool isHovered)
        {
            if (card?.Definition == null)
            {
                return;
            }

            WeaponDefinition def = card.Definition;
            Color catColor = UiHelpers.GetCategoryColor(def.Category);
            Color catDark = UiHelpers.DarkenColor(catColor, 0.72f);

            Rectangle drawRect = isHovered ? rect with { Y = rect.Y - 6 } : rect;
            UiHelpers.DrawRoundedBox(g, drawRect with { X = drawRect.X + 3, Y = drawRect.Y + 5 }, 8, Color.FromArgb(115, 0, 0, 0), Color.Transparent, 0f);
            UiHelpers.DrawGradientRoundedBox(
                g,
                drawRect,
                8,
                card.IsReady ? Color.FromArgb(40, 49, 66) : Color.FromArgb(29, 34, 44),
                card.IsReady ? Color.FromArgb(28, 35, 48) : Color.FromArgb(22, 26, 35),
                isHovered ? UiTheme.Gold : (card.IsReady ? catColor : UiTheme.Border),
                isHovered ? 2.3f : 1.5f);

            UiHelpers.DrawCardHeader(
                g,
                drawRect,
                24,
                6,
                card.IsReady ? catColor : Color.FromArgb(95, catColor.R, catColor.G, catColor.B),
                card.IsReady ? catDark : Color.FromArgb(80, catDark.R, catDark.G, catDark.B));

            EntityGraphics.DrawCategoryIcon(g, def.Category, drawRect.X + 13, drawRect.Y + 14, 11);
            if (card.HasPoison)
            {
                EntityGraphics.DrawPoisonVialIcon(g, drawRect.Right - 13, drawRect.Y + 14, 12);
            }

            DrawHandCardText(g, engine, card, drawRect, canEquip);
        }

        private static void DrawHandCardText(Graphics g, GameEngine engine, CardInstance card, Rectangle drawRect, bool canEquip)
        {
            WeaponDefinition def = card.Definition;
            using Font nameFont = new("Segoe UI", 7.8f, FontStyle.Bold);
            using Font statFont = new("Segoe UI", 7.8f, FontStyle.Bold);
            using Font modFont = new("Segoe UI", 6.9f, FontStyle.Bold);
            using Font descFont = new("Segoe UI", 7.1f, FontStyle.Regular);
            using SolidBrush whiteBrush = new(Color.White);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush cyanBrush = new(UiTheme.Cyan);
            using SolidBrush greenBrush = new(Color.FromArgb(128, 232, 166));
            using SolidBrush textBrush = new(card.IsReady ? UiTheme.TextPrimary : UiTheme.Muted);
            using SolidBrush footerBrush = new(canEquip ? UiTheme.Gold : UiTheme.Muted);
            using StringFormat centerSf = UiHelpers.CreateStringFormat();

            g.DrawString(def.Name, nameFont, whiteBrush, new Rectangle(drawRect.X + 22, drawRect.Y + 2, drawRect.Width - 40, 24), centerSf);
            g.DrawString($"DMG {engine.GetEffectiveCardDamage(card)} | CD {card.CurrentCooldown}/{engine.GetEffectiveCardMaxCooldown(card)}", statFont, card.IsReady ? goldBrush : cyanBrush, new Rectangle(drawRect.X + 4, drawRect.Y + 28, drawRect.Width - 8, 18), centerSf);

            int descTop = drawRect.Y + 47;
            if (card.AppliedModifiers.Count > 0)
            {
                g.DrawString(string.Join(" ", card.AppliedModifiers), modFont, greenBrush, new Rectangle(drawRect.X + 4, descTop, drawRect.Width - 8, 14), centerSf);
                descTop += 15;
            }

            g.DrawString(def.Description, descFont, textBrush, new Rectangle(drawRect.X + 5, descTop, drawRect.Width - 10, (drawRect.Bottom - 22) - descTop));

            string footerText = canEquip ? "CLICK TO EQUIP (1T)" : (!card.IsReady ? $"COOLDOWN: {card.CurrentCooldown}T" : "QUEUE FULL");
            g.DrawString(footerText, modFont, footerBrush, new Rectangle(drawRect.X + 4, drawRect.Bottom - 20, drawRect.Width - 8, 16), centerSf);
        }

        private static void DrawEquippedCard(
            Graphics g,
            GameEngine engine,
            CardInstance card,
            Rectangle rect,
            int slotIndex,
            bool isExecutingNow,
            bool isGhost,
            bool isDimmedSource)
        {
            if (card?.Definition == null)
            {
                return;
            }

            WeaponDefinition def = card.Definition;
            Color catColor = UiHelpers.GetCategoryColor(def.Category);
            int alpha = isGhost ? 215 : (isDimmedSource ? 105 : 255);

            Color bg = Color.FromArgb(alpha, UiTheme.CardBg.R, UiTheme.CardBg.G, UiTheme.CardBg.B);
            Color border = isExecutingNow ? UiTheme.Gold : Color.FromArgb(alpha, catColor.R, catColor.G, catColor.B);
            UiHelpers.DrawRoundedBox(g, rect, 8, bg, border, isExecutingNow ? 2.8f : 1.6f);

            Color headerColor = Color.FromArgb(alpha, catColor.R, catColor.G, catColor.B);
            UiHelpers.DrawCardHeader(g, rect, 24, 6, headerColor, headerColor);

            if (card.HasPoison && !isDimmedSource)
            {
                EntityGraphics.DrawPoisonVialIcon(g, rect.Right - 11, rect.Y + 14, 10);
            }

            using Font nameFont = new("Segoe UI", 7.3f, FontStyle.Bold);
            using Font statFont = new("Segoe UI", 7.2f, FontStyle.Bold);
            using Font descFont = new("Segoe UI", 6.7f, FontStyle.Regular);
            using SolidBrush whiteBrush = new(Color.FromArgb(alpha, 255, 255, 255));
            using SolidBrush goldBrush = new(Color.FromArgb(alpha, UiTheme.Gold.R, UiTheme.Gold.G, UiTheme.Gold.B));
            using SolidBrush textBrush = new(Color.FromArgb(alpha, UiTheme.TextSecondary.R, UiTheme.TextSecondary.G, UiTheme.TextSecondary.B));
            using StringFormat centerSf = UiHelpers.CreateStringFormat();

            g.DrawString($"#{slotIndex + 1} {def.Name}", nameFont, whiteBrush, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, 24), centerSf);
            g.DrawString($"DMG {engine.GetEffectiveCardDamage(card)} | CD {engine.GetEffectiveCardMaxCooldown(card)}", statFont, goldBrush, new Rectangle(rect.X + 4, rect.Y + 28, rect.Width - 8, 16), centerSf);

            int descTop = rect.Y + 46;
            if (card.AppliedModifiers.Count > 0)
            {
                using SolidBrush greenBrush = new(Color.FromArgb(alpha, 128, 232, 166));
                g.DrawString(string.Join(" ", card.AppliedModifiers), descFont, greenBrush, new Rectangle(rect.X + 4, descTop, rect.Width - 8, 14), centerSf);
                descTop += 14;
            }

            g.DrawString(def.Description, descFont, textBrush, new Rectangle(rect.X + 4, descTop, rect.Width - 8, (rect.Bottom - 20) - descTop));
            g.DrawString(isExecutingNow ? "ACTIVE AIM" : "DRAG TO SWAP", statFont, isExecutingNow ? goldBrush : textBrush, new Rectangle(rect.X + 4, rect.Bottom - 19, rect.Width - 8, 15), centerSf);
        }

        private static List<string> BuildUpgradeSummaryLines(GameEngine engine)
        {
            List<string> items = [];
            if (engine.GetMaxHpBonus() > 0) items.Add($"+{engine.GetMaxHpBonus()} HP");
            if (engine.GetMovementCooldownReduction() > 0) items.Add($"-{engine.GetMovementCooldownReduction()} Move CD");
            if (engine.GetFreeRerollBonus() > 0) items.Add($"{engine.GetFreeRerollBonus()} Free Reroll");
            if (engine.GetMeleeDamageBonus() > 0) items.Add($"+{engine.GetMeleeDamageBonus()} Melee DMG");
            if (engine.GetWaitEquippedCooldownBonus() > 0) items.Add("Wait -1 CD");
            if (engine.GetRangedDamageBonus() > 0) items.Add($"+{engine.GetRangedDamageBonus()} Ranged DMG");
            if (engine.GetSpecialDamageBonus() > 0) items.Add($"+{engine.GetSpecialDamageBonus()} Special DMG");
            return items;
        }

        private static void DrawLegendRow(Graphics g, int x, int y, Color swatchColor, string text, Font font, Brush textBrush)
        {
            Rectangle swatch = new(x, y + 2, 14, 14);
            UiHelpers.DrawRoundedBox(g, swatch, 3, Color.FromArgb(145, swatchColor.R, swatchColor.G, swatchColor.B), swatchColor, 1.2f);
            g.DrawString(text, font, textBrush, x + 22, y);
        }

        private (int StartX, int StartY) GetGridOrigin()
        {
            int gridTotalSize = (GridSize * TileSize) + ((GridSize - 1) * TileGap);
            return (
                _arenaFrameRect.X + ((_arenaFrameRect.Width - gridTotalSize) / 2),
                _arenaFrameRect.Y + ((_arenaFrameRect.Height - gridTotalSize) / 2));
        }

        private static Rectangle GetTileRect(int startX, int startY, int col, int row) => new(
            startX + (col * (TileSize + TileGap)),
            startY + (row * (TileSize + TileGap)),
            TileSize,
            TileSize);

        private void ResetFrameState()
        {
            _hitboxes.Clear();
            _equippedSlotRects.Clear();
        }

        private void AddHitbox(
            Rectangle bounds,
            ArenaActionType actionType,
            GridPos tilePos = default,
            int index = 0,
            string targetId = null,
            bool isEnabled = false)
        {
            _hitboxes.Add(new ArenaHitbox
            {
                Bounds = bounds,
                ActionType = actionType,
                TilePos = tilePos,
                Index = index,
                TargetId = targetId,
                IsEnabled = isEnabled
            });
        }
    }
}