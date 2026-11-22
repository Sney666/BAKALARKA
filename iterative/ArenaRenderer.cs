
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
        RewardSkip
    }

    public class ArenaHitbox
    {
        public Rectangle Bounds { get; set; }
        public ArenaActionType ActionType { get; set; }
        public GridPos TilePos { get; set; }
        public int Index { get; set; }
        public string TargetId { get; set; }
        public UpgradeModifierType ModifierType { get; set; }
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
        private readonly List<Rectangle> equippedSlotRects = new List<Rectangle>();

        private readonly Color colorBgTop = Color.FromArgb(15, 19, 26);
        private readonly Color colorBgBottom = Color.FromArgb(23, 29, 40);
        private readonly Color colorTopBar = Color.FromArgb(20, 25, 34);
        private readonly Color colorPanel = Color.FromArgb(27, 33, 44);
        private readonly Color colorCardBg = Color.FromArgb(34, 42, 56);
        private readonly Color colorBorder = Color.FromArgb(58, 70, 90);
        private readonly Color colorGold = Color.FromArgb(228, 174, 62);
        private readonly Color colorCyan = Color.FromArgb(72, 189, 214);
        private readonly Color colorRed = Color.FromArgb(212, 74, 74);
        private readonly Color colorOrange = Color.FromArgb(232, 128, 48);
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

        public int HitTestEquippedSlotIndex(Point point)
        {
            for (int i = 0; i < equippedSlotRects.Count; i++)
            {
                if (equippedSlotRects[i].Contains(point))
                {
                    return i;
                }
            }

            return -1;
        }

        public bool TryGetTileAtPoint(Point point, out GridPos tilePos)
        {
            tilePos = new GridPos(-1, -1);

            int gridTotalSize = (GridSize * TileSize) + ((GridSize - 1) * TileGap);
            int startX = arenaFrameRect.X + ((arenaFrameRect.Width - gridTotalSize) / 2);
            int startY = arenaFrameRect.Y + ((arenaFrameRect.Height - gridTotalSize) / 2);

            for (int row = 0; row < GridSize; row++)
            {
                for (int col = 0; col < GridSize; col++)
                {
                    Rectangle tileRect = new Rectangle(
                        startX + (col * (TileSize + TileGap)),
                        startY + (row * (TileSize + TileGap)),
                        TileSize,
                        TileSize);

                    if (tileRect.Contains(point))
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
            AimPreviewResult aimPreview,
            int dragSourceIndex,
            int dragHoverTargetIndex)
        {
            hitboxes.Clear();
            equippedSlotRects.Clear();

            if (engine == null || engine.CurrentRun == null)
            {
                return;
            }

            DrawBackground(g);
            DrawTopStatusBar(g, engine, profile, mouseLocation);
            DrawLeftTacticalPanel(g, engine);
            DrawArenaGrid(g, engine, mouseLocation, specialAnimProgress, aimPreview);
            DrawRightCombatLogPanel(g, engine);
            DrawBottomDock(g, engine, mouseLocation, dragSourceIndex, dragHoverTargetIndex);

            if (dragSourceIndex >= 0 &&
                dragSourceIndex < engine.CurrentRun.Player.EquippedQueue.Count)
            {
                CardInstance draggedCard = engine.CurrentRun.Player.EquippedQueue[dragSourceIndex];
                Rectangle ghostRect = new Rectangle(mouseLocation.X - 56, mouseLocation.Y - 80, 112, 160);
                DrawEquippedCard(g, engine, draggedCard, ghostRect, dragSourceIndex, false, true, false);
            }
        }

        public void RenderStartingDraft(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation)
        {
            hitboxes.Clear();
            equippedSlotRects.Clear();

            if (engine == null || engine.CurrentRun == null || engine.CurrentRun.PendingReward == null)
            {
                return;
            }

            DrawBackground(g);
            DrawTopStatusBar(g, engine, profile, mouseLocation);

            PendingRewardState draft = engine.CurrentRun.PendingReward;
            int requiredCount = Math.Min(2, draft.DraftCardIds.Count);

            using (Font titleFont = new Font("Segoe UI", 22f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 11.5f, FontStyle.Regular))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString("BUILD YOUR STARTING HAND", titleFont, goldBrush, new Rectangle(40, 74, WindowWidth - 80, 42), sf);
                g.DrawString(
                    $"Choose {requiredCount} cards from {draft.DraftCardIds.Count} unlocked cards before Wave 1 begins   (Selected: {draft.DraftSelectedIds.Count} / {requiredCount})",
                    subFont,
                    mutedBrush,
                    new Rectangle(40, 118, WindowWidth - 80, 28),
                    sf);
            }

            int count = draft.DraftCardIds.Count;
            int cardW = 276;
            int cardH = 380;
            int gap = 22;
            int totalW = (count * cardW) + (Math.Max(0, count - 1) * gap);
            int startX = (WindowWidth - totalW) / 2;
            int cardY = 168;

            for (int i = 0; i < count; i++)
            {
                string weaponId = draft.DraftCardIds[i];
                if (!engine.Database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
                {
                    continue;
                }

                Rectangle cardRect = new Rectangle(startX + (i * (cardW + gap)), cardY, cardW, cardH);
                int selIndex = draft.DraftSelectedIds.IndexOf(weaponId);
                bool isSelected = selIndex >= 0;
                bool isHovered = cardRect.Contains(mouseLocation);

                string actionLabel = isSelected ? $"SELECTED (#{selIndex + 1})" : "CLICK TO SELECT";
                DrawLargeWeaponCard(g, engine, weapon, null, cardRect, isSelected, isHovered, actionLabel);

                hitboxes.Add(new ArenaHitbox
                {
                    Bounds = cardRect,
                    ActionType = ArenaActionType.DraftToggleCard,
                    TargetId = weaponId,
                    IsEnabled = true
                });
            }

            bool canConfirm = draft.DraftSelectedIds.Count >= requiredCount;
            Rectangle confirmBtn = new Rectangle((WindowWidth / 2) - 210, 586, 420, 56);
            bool hoverConfirm = confirmBtn.Contains(mouseLocation) && canConfirm;

            if (canConfirm)
            {
                DrawGradientButton(
                    g,
                    confirmBtn,
                    hoverConfirm ? Color.FromArgb(240, 188, 76) : Color.FromArgb(208, 154, 44),
                    hoverConfirm ? Color.FromArgb(198, 142, 34) : Color.FromArgb(166, 116, 26),
                    colorGold,
                    "CONFIRM STARTING HAND & BEGIN WAVE 1",
                    12f,
                    Color.FromArgb(18, 20, 26));
            }
            else
            {
                DrawRoundedBox(g, confirmBtn, 9, Color.FromArgb(32, 37, 48), Color.FromArgb(62, 70, 86), 1.5f);
                using (Font btnFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString($"SELECT {requiredCount - draft.DraftSelectedIds.Count} MORE CARD(S)", btnFont, mutedBrush, confirmBtn, sf);
                }
            }

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = confirmBtn,
                ActionType = ArenaActionType.DraftConfirm,
                IsEnabled = canConfirm
            });
        }

        public void RenderRewardCard(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation)
        {
            hitboxes.Clear();
            equippedSlotRects.Clear();

            if (engine == null || engine.CurrentRun == null || engine.CurrentRun.PendingReward == null)
            {
                return;
            }

            DrawBackground(g);
            DrawTopStatusBar(g, engine, profile, mouseLocation);

            PendingRewardState reward = engine.CurrentRun.PendingReward;

            using (Font titleFont = new Font("Segoe UI", 22f, FontStyle.Bold))
            using (Font badgeFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(128, 232, 166)))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString("WAVE CLEARED — PICK A CARD", titleFont, goldBrush, new Rectangle(40, 72, WindowWidth - 80, 40), sf);

                Rectangle resetPill = new Rectangle((WindowWidth / 2) - 240, 118, 480, 28);
                DrawRoundedBox(g, resetPill, 7, Color.FromArgb(26, 56, 42), colorGreen, 1.2f);
                g.DrawString("ALL CARD & SPECIAL COOLDOWNS RESET TO 0!", badgeFont, greenBrush, resetPill, sf);
            }

            int count = reward.DraftCardIds.Count;
            int cardW = 340;
            int cardH = 382;
            int gap = 52;
            int totalW = (count * cardW) + (Math.Max(0, count - 1) * gap);
            int startX = (WindowWidth - totalW) / 2;
            int cardY = 178;

            string[] headers = new string[] { "OFFENSE POOL (MELEE / RANGED)", "UTILITY POOL (MOVEMENT / SPECIAL)" };

            using (Font poolFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (StringFormat sf = CreateCenterFormat())
            {
                for (int i = 0; i < count; i++)
                {
                    string weaponId = reward.DraftCardIds[i];
                    if (!engine.Database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
                    {
                        continue;
                    }

                    Rectangle cardRect = new Rectangle(startX + (i * (cardW + gap)), cardY, cardW, cardH);
                    if (i < headers.Length)
                    {
                        g.DrawString(headers[i], poolFont, cyanBrush, new Rectangle(cardRect.X, cardY - 26, cardW, 22), sf);
                    }

                    bool isHovered = cardRect.Contains(mouseLocation);
                    DrawLargeWeaponCard(g, engine, weapon, null, cardRect, false, isHovered, "ADD TO HAND");

                    hitboxes.Add(new ArenaHitbox
                    {
                        Bounds = cardRect,
                        ActionType = ArenaActionType.RewardPickCard,
                        TargetId = weaponId,
                        IsEnabled = true
                    });
                }
            }

            DrawRewardFooterButtons(g, engine, profile, mouseLocation, 600);
        }

        public void RenderRewardUpgrade(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation,
            int selectedModifierIndex,
            int selectedCardInstanceId)
        {
            hitboxes.Clear();
            equippedSlotRects.Clear();

            if (engine == null || engine.CurrentRun == null || engine.CurrentRun.PendingReward == null)
            {
                return;
            }

            DrawBackground(g);
            DrawTopStatusBar(g, engine, profile, mouseLocation);

            PendingRewardState reward = engine.CurrentRun.PendingReward;

            using (Font titleFont = new Font("Segoe UI", 20f, FontStyle.Bold))
            using (Font badgeFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(128, 232, 166)))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString("WAVE CLEARED — UPGRADE A CARD", titleFont, goldBrush, new Rectangle(40, 66, WindowWidth - 80, 36), sf);

                Rectangle resetPill = new Rectangle((WindowWidth / 2) - 230, 106, 460, 24);
                DrawRoundedBox(g, resetPill, 6, Color.FromArgb(26, 56, 42), colorGreen, 1.2f);
                g.DrawString("ALL CARD & SPECIAL COOLDOWNS RESET TO 0!", badgeFont, greenBrush, resetPill, sf);

                g.DrawString("STEP 1: CHOOSE AN UPGRADE MODIFIER", badgeFont, cyanBrush, new Rectangle(40, 138, WindowWidth - 80, 22), sf);
            }

            int modCount = reward.UpgradeOptions.Count;
            int modW = 350;
            int modH = 114;
            int modGap = 24;
            int modTotalW = (modCount * modW) + (Math.Max(0, modCount - 1) * modGap);
            int modStartX = (WindowWidth - modTotalW) / 2;
            int modY = 164;

            for (int i = 0; i < modCount; i++)
            {
                UpgradeOption option = reward.UpgradeOptions[i];
                Rectangle modRect = new Rectangle(modStartX + (i * (modW + modGap)), modY, modW, modH);
                bool isSelected = (i == selectedModifierIndex);
                bool isHovered = modRect.Contains(mouseLocation);

                Color border = isSelected ? colorGold : (isHovered ? colorCyan : colorBorder);
                Color bg = isSelected ? Color.FromArgb(42, 54, 74) : colorCardBg;
                DrawRoundedBox(g, modRect, 10, bg, border, isSelected ? 2.5f : 1.5f);

                using (Font nameFont = new Font("Segoe UI", 12.5f, FontStyle.Bold))
                using (Font descFont = new Font("Segoe UI", 9.2f, FontStyle.Regular))
                using (SolidBrush titleBrush = new SolidBrush(option.ModifierType == UpgradeModifierType.PoisonEffect ? colorGreen : colorGold))
                using (SolidBrush textBrush = new SolidBrush(colorTextPrimary))
                {
                    g.DrawString(option.Name, nameFont, titleBrush, modRect.X + 14, modRect.Y + 12);
                    g.DrawString(option.Description, descFont, textBrush, new Rectangle(modRect.X + 14, modRect.Y + 42, modRect.Width - 28, 62));
                }

                hitboxes.Add(new ArenaHitbox
                {
                    Bounds = modRect,
                    ActionType = ArenaActionType.RewardSelectModifier,
                    Index = i,
                    ModifierType = option.ModifierType,
                    IsEnabled = true
                });
            }

            using (Font stepFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString("STEP 2: CHOOSE ONE OF YOUR CARDS TO ATTACH IT TO", stepFont, cyanBrush, new Rectangle(40, 292, WindowWidth - 80, 22), sf);
            }

            List<CardInstance> ownedCards = engine.GetAllOwnedCards();
            int cardCount = ownedCards.Count;
            int cardW = 266;
            int cardH = 268;
            int cardGap = 18;
            int cardTotalW = (cardCount * cardW) + (Math.Max(0, cardCount - 1) * cardGap);
            int cardStartX = (WindowWidth - cardTotalW) / 2;
            int cardY = 320;

            for (int i = 0; i < cardCount; i++)
            {
                CardInstance card = ownedCards[i];
                Rectangle cardRect = new Rectangle(cardStartX + (i * (cardW + cardGap)), cardY, cardW, cardH);
                bool isSelected = (card.InstanceId == selectedCardInstanceId);
                bool isHovered = cardRect.Contains(mouseLocation);
                string actionLabel = isSelected ? "TARGET SELECTED" : "CLICK TO SELECT";

                DrawLargeWeaponCard(g, engine, card.Definition, card, cardRect, isSelected, isHovered, actionLabel);

                hitboxes.Add(new ArenaHitbox
                {
                    Bounds = cardRect,
                    ActionType = ArenaActionType.RewardSelectTargetCard,
                    Index = card.InstanceId,
                    IsEnabled = true
                });
            }

            bool canApply = selectedModifierIndex >= 0 &&
                            selectedModifierIndex < reward.UpgradeOptions.Count &&
                            ownedCards.Any(c => c.InstanceId == selectedCardInstanceId);

            Rectangle applyBtn = new Rectangle((WindowWidth / 2) - 430, 616, 300, 48);
            bool hoverApply = applyBtn.Contains(mouseLocation) && canApply;

            if (canApply)
            {
                DrawGradientButton(
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
                DrawRoundedBox(g, applyBtn, 8, Color.FromArgb(32, 37, 48), Color.FromArgb(58, 66, 82), 1.5f);
                using (Font btnFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString("SELECT MODIFIER & CARD", btnFont, mutedBrush, applyBtn, sf);
                }
            }

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = applyBtn,
                ActionType = ArenaActionType.RewardApplyUpgrade,
                IsEnabled = canApply
            });

            DrawRewardFooterButtonsRight(g, engine, profile, mouseLocation, 616);
        }

        private void DrawRewardFooterButtons(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation,
            int y)
        {
            PendingRewardState reward = engine.CurrentRun.PendingReward;
            bool hasFreeReroll = engine.CurrentRun.Player.FreeRerollsRemaining > 0;
            bool canAfford = hasFreeReroll || profile.Coins >= reward.NextRerollCost;

            Rectangle rerollBtn = new Rectangle((WindowWidth / 2) - 290, y, 276, 48);
            bool hoverReroll = rerollBtn.Contains(mouseLocation) && canAfford;

            string rerollLabel = hasFreeReroll
                ? $"REROLL (FREE — CHARISMA)"
                : $"REROLL ({reward.NextRerollCost} COINS)";

            if (canAfford)
            {
                DrawGradientButton(
                    g,
                    rerollBtn,
                    hoverReroll ? Color.FromArgb(236, 184, 72) : Color.FromArgb(204, 150, 42),
                    hoverReroll ? Color.FromArgb(196, 140, 34) : Color.FromArgb(164, 112, 24),
                    colorGold,
                    rerollLabel,
                    10.2f,
                    Color.FromArgb(18, 20, 26));
            }
            else
            {
                DrawRoundedBox(g, rerollBtn, 8, Color.FromArgb(32, 37, 48), Color.FromArgb(58, 66, 82), 1.5f);
                using (Font btnFont = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString($"REROLL (NEED {reward.NextRerollCost} COINS)", btnFont, mutedBrush, rerollBtn, sf);
                }
            }

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = rerollBtn,
                ActionType = ArenaActionType.RewardReroll,
                IsEnabled = canAfford
            });

            Rectangle skipBtn = new Rectangle((WindowWidth / 2) + 14, y, 276, 48);
            bool hoverSkip = skipBtn.Contains(mouseLocation);
            DrawRoundedBox(
                g,
                skipBtn,
                8,
                hoverSkip ? Color.FromArgb(56, 68, 88) : Color.FromArgb(38, 46, 61),
                hoverSkip ? colorCyan : colorBorder,
                1.5f);

            using (Font btnFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush whiteBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString("SKIP REWARD", btnFont, whiteBrush, skipBtn, sf);
            }

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = skipBtn,
                ActionType = ArenaActionType.RewardSkip,
                IsEnabled = true
            });
        }

        private void DrawRewardFooterButtonsRight(
            Graphics g,
            GameEngine engine,
            PlayerProfile profile,
            Point mouseLocation,
            int y)
        {
            PendingRewardState reward = engine.CurrentRun.PendingReward;
            bool hasFreeReroll = engine.CurrentRun.Player.FreeRerollsRemaining > 0;
            bool canAfford = hasFreeReroll || profile.Coins >= reward.NextRerollCost;

            Rectangle rerollBtn = new Rectangle((WindowWidth / 2) - 108, y, 270, 48);
            bool hoverReroll = rerollBtn.Contains(mouseLocation) && canAfford;

            string rerollLabel = hasFreeReroll
                ? "REROLL (FREE — CHARISMA)"
                : $"REROLL ({reward.NextRerollCost} COINS)";

            if (canAfford)
            {
                DrawGradientButton(
                    g,
                    rerollBtn,
                    hoverReroll ? Color.FromArgb(236, 184, 72) : Color.FromArgb(204, 150, 42),
                    hoverReroll ? Color.FromArgb(196, 140, 34) : Color.FromArgb(164, 112, 24),
                    colorGold,
                    rerollLabel,
                    10f,
                    Color.FromArgb(18, 20, 26));
            }
            else
            {
                DrawRoundedBox(g, rerollBtn, 8, Color.FromArgb(32, 37, 48), Color.FromArgb(58, 66, 82), 1.5f);
                using (Font btnFont = new Font("Segoe UI", 9.8f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString($"REROLL (NEED {reward.NextRerollCost} COINS)", btnFont, mutedBrush, rerollBtn, sf);
                }
            }

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = rerollBtn,
                ActionType = ArenaActionType.RewardReroll,
                IsEnabled = canAfford
            });

            Rectangle skipBtn = new Rectangle((WindowWidth / 2) + 182, y, 248, 48);
            bool hoverSkip = skipBtn.Contains(mouseLocation);
            DrawRoundedBox(
                g,
                skipBtn,
                8,
                hoverSkip ? Color.FromArgb(56, 68, 88) : Color.FromArgb(38, 46, 61),
                hoverSkip ? colorCyan : colorBorder,
                1.5f);

            using (Font btnFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush whiteBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString("SKIP REWARD", btnFont, whiteBrush, skipBtn, sf);
            }

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = skipBtn,
                ActionType = ArenaActionType.RewardSkip,
                IsEnabled = true
            });
        }

        private void DrawLargeWeaponCard(
            Graphics g,
            GameEngine engine,
            WeaponDefinition def,
            CardInstance instance,
            Rectangle rect,
            bool isSelected,
            bool isHovered,
            string footerActionLabel)
        {
            Color catColor = GetCategoryColor(def.Category);
            Rectangle drawRect = (isHovered || isSelected)
                ? new Rectangle(rect.X, rect.Y - 5, rect.Width, rect.Height)
                : rect;

            Rectangle shadow = new Rectangle(drawRect.X + 5, drawRect.Y + 6, drawRect.Width, drawRect.Height);
            DrawRoundedBox(g, shadow, 12, Color.FromArgb(120, 0, 0, 0), Color.Transparent, 0f);

            Color borderColor = isSelected ? colorGold : (isHovered ? catColor : colorBorder);
            DrawRoundedBox(g, drawRect, 12, colorCardBg, borderColor, isSelected ? 3f : 1.8f);

            Rectangle headerRect = new Rectangle(drawRect.X + 2, drawRect.Y + 2, drawRect.Width - 4, 42);
            using (GraphicsPath headerPath = CreateTopRoundedRectPath(headerRect, 10))
            using (SolidBrush headerBrush = new SolidBrush(catColor))
            {
                g.FillPath(headerBrush, headerPath);
            }

            CardInstance previewCard = instance ?? new CardInstance { Definition = def, DefinitionId = def.Id };
            int effectiveDmg = engine.GetEffectiveCardDamage(previewCard);
            int effectiveMaxCd = engine.GetEffectiveCardMaxCooldown(previewCard);

            using (Font nameFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (Font badgeFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Font descFont = new Font("Segoe UI", 9.4f, FontStyle.Regular))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            using (SolidBrush primaryBrush = new SolidBrush(colorTextPrimary))
            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(132, 234, 166)))
            using (StringFormat centerSf = CreateCenterFormat())
            {
                g.DrawString(def.Name, nameFont, whiteBrush, drawRect.X + 12, drawRect.Y + 11);

                Rectangle catPill = new Rectangle(drawRect.Right - 88, drawRect.Y + 10, 76, 22);
                DrawRoundedBox(g, catPill, 5, Color.FromArgb(95, 0, 0, 0), Color.FromArgb(150, 255, 255, 255), 1f);
                g.DrawString(def.Category.ToString().ToUpperInvariant(), badgeFont, whiteBrush, catPill, centerSf);

                int pillY = drawRect.Y + 54;
                int pillW = (drawRect.Width - 36) / 3;
                DrawStatPill(g, new Rectangle(drawRect.X + 12, pillY, pillW, 28), $"DMG: {effectiveDmg}", Color.FromArgb(236, 114, 106));
                DrawStatPill(g, new Rectangle(drawRect.X + 18 + pillW, pillY, pillW, 28), $"CD: {effectiveMaxCd}T", colorCyan);
                DrawStatPill(g, new Rectangle(drawRect.X + 24 + (pillW * 2), pillY, pillW, 28), def.Targeting.ToString(), colorGold);

                int descTop = pillY + 38;
                if (instance != null && instance.AppliedModifiers.Count > 0)
                {
                    Rectangle modRect = new Rectangle(drawRect.X + 12, descTop, drawRect.Width - 24, 24);
                    DrawRoundedBox(g, modRect, 5, Color.FromArgb(28, 48, 40), colorGreen, 1f);
                    g.DrawString($"Mods: {string.Join(", ", instance.AppliedModifiers)}", badgeFont, greenBrush, modRect, centerSf);
                    descTop += 30;
                }

                int descHeight = Math.Max(60, (drawRect.Bottom - 56) - descTop);
                Rectangle descBox = new Rectangle(drawRect.X + 12, descTop, drawRect.Width - 24, descHeight);
                DrawRoundedBox(g, descBox, 7, Color.FromArgb(24, 30, 41), Color.FromArgb(46, 56, 73), 1f);
                g.DrawString(def.Description, descFont, primaryBrush, new Rectangle(descBox.X + 10, descBox.Y + 8, descBox.Width - 20, descBox.Height - 16));

                Rectangle footerBtn = new Rectangle(drawRect.X + 12, drawRect.Bottom - 44, drawRect.Width - 24, 32);
                Color footerBg = isSelected ? Color.FromArgb(186, 134, 36) : Color.FromArgb(44, 54, 72);
                DrawRoundedBox(g, footerBtn, 6, footerBg, isSelected ? colorGold : catColor, 1.2f);
                using var textBrush = new SolidBrush(isSelected ? Color.FromArgb(18, 20, 26) : Color.White);
                g.DrawString(footerActionLabel, badgeFont, textBrush, footerBtn, centerSf);
            }
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

                DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 38, colorCyan, "Cyan Tile: Move / Movement Card Aim Preview", itemFont, textBrush);
                DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 62, colorOrange, "Orange/Red Tile: Weapon Aim Preview / Enemy Attack", itemFont, textBrush);
                DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 86, colorGreen, "Green Toxic Glow: Poison Ground Hazard", itemFont, textBrush);
                DrawLegendRow(g, legendPanel.X + 14, legendPanel.Y + 110, colorGold, "Spiked Ring: Bear Trap Hazard (Arms on Leave)", itemFont, textBrush);
            }

            Rectangle controlsPanel = new Rectangle(20, 400, 384, 98);
            DrawRoundedBox(g, controlsPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Font ctrlFont = new Font("Segoe UI", 8.7f, FontStyle.Regular))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            {
                g.DrawString("CONTROLS", headerFont, goldBrush, controlsPanel.X + 14, controlsPanel.Y + 9);
                g.DrawString("Move: WASD / Arrows / Click Adjacent  |  Wait: Space  |  Special: E", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 33);
                g.DrawString("Equip Card: Click Ready Hand Card (1T)  |  Reorder: Drag Queue (0T)", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 53);
                g.DrawString("Play Queue: Enter / Button  |  Aim: Hover & Click (or Keys + Enter)", ctrlFont, mutedBrush, controlsPanel.X + 14, controlsPanel.Y + 73);
            }
        }

        private void DrawArenaGrid(
            Graphics g,
            GameEngine engine,
            Point mouseLocation,
            float specialAnimProgress,
            AimPreviewResult aimPreview)
        {
            Color frameBorder = engine.IsExecutingQueue ? colorOrange : colorGold;
            DrawRoundedBox(g, arenaFrameRect, 12, Color.FromArgb(22, 27, 37), frameBorder, 2.2f);

            int gridTotalSize = (GridSize * TileSize) + ((GridSize - 1) * TileGap);
            int startX = arenaFrameRect.X + ((arenaFrameRect.Width - gridTotalSize) / 2);
            int startY = arenaFrameRect.Y + ((arenaFrameRect.Height - gridTotalSize) / 2);

            List<GridPos> validMoves = engine.GetValidMoveTiles();
            HashSet<GridPos> validMoveSet = new HashSet<GridPos>(validMoves);

            HashSet<GridPos> aimAffected = new HashSet<GridPos>();
            HashSet<GridPos> aimHits = new HashSet<GridPos>();
            if (engine.IsExecutingQueue && aimPreview != null && aimPreview.IsValidAim)
            {
                foreach (GridPos tile in aimPreview.AffectedTiles)
                {
                    aimAffected.Add(tile);
                }

                foreach (GridPos hitTile in aimPreview.HitEnemyTiles)
                {
                    aimHits.Add(hitTile);
                }
            }

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
                    bool isValidMove = !engine.IsExecutingQueue && validMoveSet.Contains(pos);
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
                    else if (engine.IsExecutingQueue && pos != engine.CurrentRun.Player.Position)
                    {
                        hitboxes.Add(new ArenaHitbox
                        {
                            Bounds = tileRect,
                            ActionType = ArenaActionType.AimTile,
                            TilePos = pos,
                            IsEnabled = true
                        });
                    }
                }
            }

            DrawHazardsOnGrid(g, engine.CurrentRun.Hazards, startX, startY);

            if (engine.IsExecutingQueue && aimPreview != null && aimPreview.IsValidAim)
            {
                DrawAimPreviewOverlay(g, startX, startY, aimPreview, aimAffected, aimHits);
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

            if (engine.IsExecutingQueue && engine.CurrentAimingCard != null)
            {
                CardInstance activeCard = engine.CurrentAimingCard;
                Rectangle bannerRect = new Rectangle(arenaFrameRect.X + 16, arenaFrameRect.Y + 6, arenaFrameRect.Width - 32, 26);
                DrawRoundedBox(g, bannerRect, 6, Color.FromArgb(220, 44, 24, 14), colorOrange, 1.5f);
                using (Font bannerFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
                using (SolidBrush whiteBrush = new SolidBrush(Color.White))
                using (StringFormat sf = CreateCenterFormat())
                {
                    string bannerText = $"AIMING [{engine.CurrentExecutingQueueIndex + 1}/{player.EquippedQueue.Count}]: {activeCard.Definition.Name} ({activeCard.Definition.Targeting}) — Click to Confirm";
                    g.DrawString(bannerText, bannerFont, whiteBrush, bannerRect, sf);
                }
            }
        }

        private void DrawHazardsOnGrid(Graphics g, List<GroundHazard> hazards, int startX, int startY)
        {
            foreach (GroundHazard hazard in hazards)
            {
                if (!hazard.Position.IsValid(GridSize))
                {
                    continue;
                }

                Rectangle tileRect = new Rectangle(
                    startX + (hazard.Position.X * (TileSize + TileGap)),
                    startY + (hazard.Position.Y * (TileSize + TileGap)),
                    TileSize,
                    TileSize);

                int cx = tileRect.X + (tileRect.Width / 2);
                int cy = tileRect.Y + (tileRect.Height / 2);

                if (hazard.Type == HazardType.BearTrap)
                {
                    Color ringColor = hazard.IsArmed ? colorGold : colorMuted;
                    using (Pen ringPen = new Pen(ringColor, 2.5f))
                    using (SolidBrush centerBrush = new SolidBrush(hazard.IsArmed ? Color.FromArgb(180, 186, 62, 46) : Color.FromArgb(110, 90, 96, 110)))
                    {
                        g.FillEllipse(centerBrush, cx - 10, cy - 10, 20, 20);
                        g.DrawEllipse(ringPen, cx - 18, cy - 18, 36, 36);

                        for (int i = 0; i < 8; i++)
                        {
                            double angle = (Math.PI / 4.0) * i;
                            int x1 = cx + (int)(Math.Cos(angle) * 14);
                            int y1 = cy + (int)(Math.Sin(angle) * 14);
                            int x2 = cx + (int)(Math.Cos(angle) * 23);
                            int y2 = cy + (int)(Math.Sin(angle) * 23);
                            g.DrawLine(ringPen, x1, y1, x2, y2);
                        }
                    }

                    using (Font trapFont = new Font("Segoe UI", 6.8f, FontStyle.Bold))
                    using (SolidBrush labelBrush = new SolidBrush(ringColor))
                    using (StringFormat sf = CreateCenterFormat())
                    {
                        string label = hazard.IsArmed ? "TRAP ARMED" : "TRAP SET";
                        g.DrawString(label, trapFont, labelBrush, new Rectangle(tileRect.X + 2, tileRect.Bottom - 16, tileRect.Width - 4, 14), sf);
                    }
                }
                else if (hazard.Type == HazardType.Poison)
                {
                    using (SolidBrush toxicFill = new SolidBrush(Color.FromArgb(88, 62, 198, 96)))
                    using (Pen toxicBorder = new Pen(Color.FromArgb(180, 108, 236, 134), 1.6f))
                    {
                        g.FillRectangle(toxicFill, tileRect.X + 4, tileRect.Y + 4, tileRect.Width - 8, tileRect.Height - 8);
                        g.DrawEllipse(toxicBorder, cx - 16, cy - 10, 12, 12);
                        g.DrawEllipse(toxicBorder, cx + 4, cy - 16, 10, 10);
                        g.DrawEllipse(toxicBorder, cx - 2, cy + 4, 14, 14);
                    }
                }
            }
        }

        private void DrawAimPreviewOverlay(
            Graphics g,
            int startX,
            int startY,
            AimPreviewResult preview,
            HashSet<GridPos> affectedTiles,
            HashSet<GridPos> hitTiles)
        {
            foreach (GridPos pos in affectedTiles)
            {
                Rectangle tileRect = new Rectangle(
                    startX + (pos.X * (TileSize + TileGap)),
                    startY + (pos.Y * (TileSize + TileGap)),
                    TileSize,
                    TileSize);

                if (preview.IsMovement)
                {
                    bool isDest = preview.MoveDestination.HasValue && preview.MoveDestination.Value == pos;
                    Color fill = isDest ? Color.FromArgb(135, 240, 206, 66) : Color.FromArgb(85, 72, 189, 214);
                    Color border = isDest ? Color.FromArgb(255, 248, 224, 110) : colorCyan;
                    DrawRoundedBox(g, tileRect, 7, fill, border, isDest ? 2.8f : 1.8f);
                }
                else
                {
                    bool hitsEnemy = hitTiles.Contains(pos);
                    Color fill = hitsEnemy ? Color.FromArgb(150, 224, 58, 58) : Color.FromArgb(105, 235, 132, 46);
                    Color border = hitsEnemy ? Color.FromArgb(255, 255, 120, 110) : colorOrange;
                    DrawRoundedBox(g, tileRect, 7, fill, border, hitsEnemy ? 2.8f : 2f);

                    if (hitsEnemy)
                    {
                        int cx = tileRect.X + (tileRect.Width / 2);
                        int cy = tileRect.Y + (tileRect.Height / 2);
                        using (Pen crossPen = new Pen(Color.White, 2f))
                        {
                            g.DrawEllipse(crossPen, cx - 16, cy - 16, 32, 32);
                            g.DrawLine(crossPen, cx - 22, cy, cx + 22, cy);
                            g.DrawLine(crossPen, cx, cy - 22, cx, cy + 22);
                        }
                    }
                }
            }
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
            using (Font logFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
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
                int rowY = logPanel.Y + 74;
                int rowHeight = 38;

                for (int i = startIndex; i < entries.Count; i++)
                {
                    bool isLatest = (i == entries.Count - 1);
                    Rectangle rowRect = new Rectangle(logPanel.X + 14, rowY, logPanel.Width - 28, rowHeight);
                    Color rowBg = isLatest ? Color.FromArgb(38, 48, 64) : Color.FromArgb(24, 30, 41);
                    Color rowBorder = isLatest ? colorGold : Color.FromArgb(42, 51, 67);
                    DrawRoundedBox(g, rowRect, 6, rowBg, rowBorder, 1f);

                    Rectangle textRect = new Rectangle(rowRect.X + 8, rowRect.Y + 3, rowRect.Width - 16, rowRect.Height - 6);
                    g.DrawString(entries[i], logFont, isLatest ? primaryBrush : secondaryBrush, textRect);

                    rowY += rowHeight + 4;
                }

                if (!string.IsNullOrWhiteSpace(engine.LastActionFeedback))
                {
                    Rectangle feedbackRect = new Rectangle(logPanel.X + 14, logPanel.Bottom - 28, logPanel.Width - 28, 22);
                    g.DrawString(engine.LastActionFeedback, subFont, goldBrush, feedbackRect);
                }
            }
        }

        private void DrawBottomDock(
            Graphics g,
            GameEngine engine,
            Point mouseLocation,
            int dragSourceIndex,
            int dragHoverTargetIndex)
        {
            PlayerState player = engine.CurrentRun.Player;
            CharacterDefinition hero = engine.GetActiveCharacterDefinition();

            Rectangle handPanel = new Rectangle(20, 506, 616, 204);
            DrawRoundedBox(g, handPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 9.8f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            {
                g.DrawString($"HAND ({player.Hand.Count} CARDS)", headerFont, goldBrush, handPanel.X + 12, handPanel.Y + 8);
                g.DrawString("Click Ready Card to Equip (1 Turn)", new Font("Segoe UI", 8.2f, FontStyle.Regular), mutedBrush, handPanel.X + 170, handPanel.Y + 10);
            }

            int cardWidth = 144;
            int cardHeight = 164;
            int cardGap = 8;
            int cardStartX = handPanel.X + 10;
            int cardY = handPanel.Y + 32;

            for (int i = 0; i < player.Hand.Count && i < 4; i++)
            {
                CardInstance card = player.Hand[i];
                Rectangle cardRect = new Rectangle(cardStartX + (i * (cardWidth + cardGap)), cardY, cardWidth, cardHeight);
                bool canEquip = !engine.IsExecutingQueue &&
                                card.IsReady &&
                                player.EquippedQueue.Count < GameEngine.MaxEquippedSlots;
                bool isHovered = cardRect.Contains(mouseLocation) && canEquip;

                DrawHandCard(g, engine, card, cardRect, canEquip, isHovered);

                hitboxes.Add(new ArenaHitbox
                {
                    Bounds = cardRect,
                    ActionType = ArenaActionType.EquipHandCard,
                    Index = i,
                    IsEnabled = canEquip
                });
            }

            Rectangle queuePanel = new Rectangle(646, 506, 368, 204);
            DrawRoundedBox(g, queuePanel, 10, colorPanel, engine.IsExecutingQueue ? colorOrange : colorBorder, engine.IsExecutingQueue ? 2f : 1.5f);

            using (Font headerFont = new Font("Segoe UI", 9.8f, FontStyle.Bold))
            using (Font slotFont = new Font("Segoe UI", 8.4f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
            using (StringFormat sf = CreateCenterFormat())
            {
                string queueTitle = engine.IsExecutingQueue
                    ? $"EXECUTING QUEUE ({engine.CurrentExecutingQueueIndex + 1}/{player.EquippedQueue.Count})"
                    : $"EQUIPPED ({player.EquippedQueue.Count}/{GameEngine.MaxEquippedSlots}) — DRAG TO REORDER";
                g.DrawString(queueTitle, headerFont, goldBrush, queuePanel.X + 10, queuePanel.Y + 8);

                int slotW = 112;
                int slotH = 164;
                int slotGap = 8;
                int slotStartX = queuePanel.X + 8;
                int slotY = queuePanel.Y + 32;

                for (int i = 0; i < GameEngine.MaxEquippedSlots; i++)
                {
                    Rectangle slotRect = new Rectangle(slotStartX + (i * (slotW + slotGap)), slotY, slotW, slotH);
                    equippedSlotRects.Add(slotRect);

                    bool isDragTarget = (dragSourceIndex >= 0 && dragHoverTargetIndex == i && i < player.EquippedQueue.Count);

                    if (i < player.EquippedQueue.Count)
                    {
                        bool isExecutingThis = engine.IsExecutingQueue && (engine.CurrentExecutingQueueIndex == i);
                        bool isBeingDragged = (dragSourceIndex == i);

                        DrawEquippedCard(g, engine, player.EquippedQueue[i], slotRect, i, isExecutingThis, false, isBeingDragged);

                        if (isDragTarget && !isBeingDragged)
                        {
                            using (Pen dropPen = new Pen(colorGold, 2.5f))
                            {
                                dropPen.DashStyle = DashStyle.Dash;
                                g.DrawRectangle(dropPen, slotRect.X + 2, slotRect.Y + 2, slotRect.Width - 4, slotRect.Height - 4);
                            }
                        }

                        hitboxes.Add(new ArenaHitbox
                        {
                            Bounds = slotRect,
                            ActionType = ArenaActionType.EquippedSlot,
                            Index = i,
                            IsEnabled = !engine.IsExecutingQueue
                        });
                    }
                    else
                    {
                        DrawRoundedBox(g, slotRect, 8, Color.FromArgb(22, 27, 36), Color.FromArgb(48, 58, 74), 1.2f);
                        g.DrawString($"SLOT #{i + 1}\nEmpty", slotFont, mutedBrush, slotRect, sf);
                    }
                }
            }

            Rectangle actionPanel = new Rectangle(1024, 506, 236, 204);
            DrawRoundedBox(g, actionPanel, 10, colorPanel, colorBorder, 1.5f);

            using (Font headerFont = new Font("Segoe UI", 9.8f, FontStyle.Bold))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            {
                g.DrawString("TURN ACTIONS", headerFont, goldBrush, actionPanel.X + 12, actionPanel.Y + 8);
            }

            Rectangle playBtn = new Rectangle(actionPanel.X + 12, actionPanel.Y + 34, actionPanel.Width - 24, 48);
            bool canPlayQueue = !engine.IsExecutingQueue && player.EquippedQueue.Count > 0;
            bool hoverPlay = playBtn.Contains(mouseLocation) && canPlayQueue;

            if (engine.IsExecutingQueue)
            {
                bool canCancel = engine.CurrentExecutingQueueIndex == 0;
                bool hoverCancel = playBtn.Contains(mouseLocation) && canCancel;
                DrawRoundedBox(
                    g,
                    playBtn,
                    8,
                    canCancel ? (hoverCancel ? Color.FromArgb(148, 54, 54) : Color.FromArgb(114, 42, 42)) : Color.FromArgb(44, 34, 24),
                    canCancel ? colorRed : colorOrange,
                    1.5f);

                using (Font btnFont = new Font("Segoe UI", 9.2f, FontStyle.Bold))
                using (SolidBrush whiteBrush = new SolidBrush(Color.White))
                using (StringFormat sf = CreateCenterFormat())
                {
                    string lbl = canCancel ? "CANCEL AIMING [ESC]" : "AIM & CLICK TILE";
                    g.DrawString(lbl, btnFont, whiteBrush, playBtn, sf);
                }

                hitboxes.Add(new ArenaHitbox
                {
                    Bounds = playBtn,
                    ActionType = ArenaActionType.CancelAim,
                    IsEnabled = canCancel
                });
            }
            else if (canPlayQueue)
            {
                DrawGradientButton(
                    g,
                    playBtn,
                    hoverPlay ? Color.FromArgb(88, 202, 134) : Color.FromArgb(62, 166, 107),
                    hoverPlay ? Color.FromArgb(56, 158, 98) : Color.FromArgb(44, 128, 80),
                    Color.FromArgb(124, 228, 162),
                    $"PLAY QUEUE ({player.EquippedQueue.Count}) [Enter]",
                    9.5f,
                    Color.White);

                hitboxes.Add(new ArenaHitbox
                {
                    Bounds = playBtn,
                    ActionType = ArenaActionType.PlayQueue,
                    IsEnabled = true
                });
            }
            else
            {
                DrawRoundedBox(g, playBtn, 8, Color.FromArgb(30, 35, 45), Color.FromArgb(52, 60, 74), 1.2f);
                using (Font btnFont = new Font("Segoe UI", 9.2f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    g.DrawString("PLAY QUEUE (EMPTY)", btnFont, mutedBrush, playBtn, sf);
                }
            }

            Rectangle specialBtn = new Rectangle(actionPanel.X + 12, actionPanel.Y + 90, actionPanel.Width - 24, 52);
            bool specialReady = !engine.IsExecutingQueue && player.SpecialCooldown <= 0;
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
                    8.8f,
                    spText);
            }
            else
            {
                DrawRoundedBox(g, specialBtn, 8, Color.FromArgb(32, 37, 48), Color.FromArgb(58, 66, 82), 1.5f);
                using (Font btnFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
                using (SolidBrush mutedBrush = new SolidBrush(colorMuted))
                using (StringFormat sf = CreateCenterFormat())
                {
                    string cdLabel = engine.IsExecutingQueue
                        ? "AIMING IN PROGRESS"
                        : $"COOLDOWN: {player.SpecialCooldown} TURNS";
                    g.DrawString(
                        $"{hero.SpecialName.ToUpperInvariant()} [E]\n{cdLabel}",
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

            Rectangle waitBtn = new Rectangle(actionPanel.X + 12, actionPanel.Y + 150, actionPanel.Width - 24, 42);
            bool canWait = !engine.IsExecutingQueue;
            bool hoverWait = waitBtn.Contains(mouseLocation) && canWait;
            Color waitTop = canWait ? (hoverWait ? Color.FromArgb(66, 86, 114) : Color.FromArgb(46, 60, 82)) : Color.FromArgb(30, 35, 45);
            Color waitBottom = canWait ? (hoverWait ? Color.FromArgb(48, 64, 88) : Color.FromArgb(34, 44, 62)) : Color.FromArgb(26, 31, 40);
            string waitLabel = engine.GetWaitEquippedCooldownBonus() > 0
                ? "WAIT [Space] (+1 CD)"
                : "WAIT TURN [Space]";
            DrawGradientButton(
                g,
                waitBtn,
                waitTop,
                waitBottom,
                hoverWait ? colorCyan : colorBorder,
                waitLabel,
                9.2f,
                canWait ? colorTextPrimary : colorMuted);

            hitboxes.Add(new ArenaHitbox
            {
                Bounds = waitBtn,
                ActionType = ArenaActionType.WaitTurn,
                IsEnabled = canWait
            });
        }

        private void DrawHandCard(
            Graphics g,
            GameEngine engine,
            CardInstance card,
            Rectangle rect,
            bool canEquip,
            bool isHovered)
        {
            if (card == null || card.Definition == null)
            {
                return;
            }

            WeaponDefinition def = card.Definition;
            Color catColor = GetCategoryColor(def.Category);

            Rectangle drawRect = isHovered
                ? new Rectangle(rect.X, rect.Y - 5, rect.Width, rect.Height)
                : rect;

            Rectangle shadow = new Rectangle(drawRect.X + 3, drawRect.Y + 4, drawRect.Width, drawRect.Height);
            DrawRoundedBox(g, shadow, 8, Color.FromArgb(100, 0, 0, 0), Color.Transparent, 0f);

            Color border = isHovered ? colorGold : (card.IsReady ? catColor : colorBorder);
            DrawRoundedBox(g, drawRect, 8, card.IsReady ? colorCardBg : Color.FromArgb(28, 33, 43), border, isHovered ? 2.2f : 1.5f);

            Rectangle headerRect = new Rectangle(drawRect.X + 2, drawRect.Y + 2, drawRect.Width - 4, 24);
            using (GraphicsPath path = CreateTopRoundedRectPath(headerRect, 6))
            using (SolidBrush headerBrush = new SolidBrush(card.IsReady ? catColor : Color.FromArgb(90, catColor.R, catColor.G, catColor.B)))
            {
                g.FillPath(headerBrush, path);
            }

            int effectiveDamage = engine.GetEffectiveCardDamage(card);
            int effectiveMaxCd = engine.GetEffectiveCardMaxCooldown(card);

            using (Font nameFont = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (Font statFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (Font modFont = new Font("Segoe UI", 7f, FontStyle.Bold))
            using (Font descFont = new Font("Segoe UI", 7.1f, FontStyle.Regular))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            using (SolidBrush goldBrush = new SolidBrush(colorGold))
            using (SolidBrush cyanBrush = new SolidBrush(colorCyan))
            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(128, 232, 166)))
            using (SolidBrush textBrush = new SolidBrush(card.IsReady ? colorTextPrimary : colorMuted))
            using (StringFormat centerSf = CreateCenterFormat())
            {
                g.DrawString(def.Name, nameFont, whiteBrush, headerRect, centerSf);

                Rectangle statsRect = new Rectangle(drawRect.X + 4, drawRect.Y + 28, drawRect.Width - 8, 18);
                g.DrawString($"DMG {effectiveDamage} | CD {card.CurrentCooldown}/{effectiveMaxCd}", statFont, card.IsReady ? goldBrush : cyanBrush, statsRect, centerSf);

                int descTop = drawRect.Y + 47;
                if (card.AppliedModifiers.Count > 0)
                {
                    Rectangle modRect = new Rectangle(drawRect.X + 4, descTop, drawRect.Width - 8, 14);
                    g.DrawString(string.Join(" ", card.AppliedModifiers), modFont, greenBrush, modRect, centerSf);
                    descTop += 15;
                }

                Rectangle descRect = new Rectangle(drawRect.X + 5, descTop, drawRect.Width - 10, (drawRect.Bottom - 22) - descTop);
                g.DrawString(def.Description, descFont, textBrush, descRect);

                Rectangle footerRect = new Rectangle(drawRect.X + 4, drawRect.Bottom - 20, drawRect.Width - 8, 16);
                string footerText = canEquip
                    ? "CLICK TO EQUIP (1T)"
                    : (!card.IsReady ? $"COOLDOWN: {card.CurrentCooldown}T" : "QUEUE FULL");
                Color footerColor = canEquip ? colorGold : colorMuted;
                using (SolidBrush footerBrush = new SolidBrush(footerColor))
                {
                    g.DrawString(footerText, modFont, footerBrush, footerRect, centerSf);
                }
            }
        }

        private void DrawEquippedCard(
            Graphics g,
            GameEngine engine,
            CardInstance card,
            Rectangle rect,
            int slotIndex,
            bool isExecutingNow,
            bool isGhost,
            bool isDimmedSource)
        {
            if (card == null || card.Definition == null)
            {
                return;
            }

            WeaponDefinition def = card.Definition;
            Color catColor = GetCategoryColor(def.Category);

            int alpha = isGhost ? 210 : (isDimmedSource ? 105 : 255);
            Color bg = Color.FromArgb(alpha, colorCardBg.R, colorCardBg.G, colorCardBg.B);
            Color border = isExecutingNow ? colorGold : Color.FromArgb(alpha, catColor.R, catColor.G, catColor.B);

            DrawRoundedBox(g, rect, 8, bg, border, isExecutingNow ? 2.8f : 1.6f);

            Rectangle headerRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, 24);
            using (GraphicsPath path = CreateTopRoundedRectPath(headerRect, 6))
            using (SolidBrush headerBrush = new SolidBrush(Color.FromArgb(alpha, catColor.R, catColor.G, catColor.B)))
            {
                g.FillPath(headerBrush, path);
            }

            int effectiveDamage = engine.GetEffectiveCardDamage(card);
            int effectiveMaxCd = engine.GetEffectiveCardMaxCooldown(card);

            using (Font nameFont = new Font("Segoe UI", 7.4f, FontStyle.Bold))
            using (Font statFont = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            using (Font descFont = new Font("Segoe UI", 6.7f, FontStyle.Regular))
            using (SolidBrush whiteBrush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
            using (SolidBrush goldBrush = new SolidBrush(Color.FromArgb(alpha, colorGold.R, colorGold.G, colorGold.B)))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(alpha, colorTextSecondary.R, colorTextSecondary.G, colorTextSecondary.B)))
            using (StringFormat centerSf = CreateCenterFormat())
            {
                g.DrawString($"#{slotIndex + 1} {def.Name}", nameFont, whiteBrush, headerRect, centerSf);

                Rectangle statsRect = new Rectangle(rect.X + 4, rect.Y + 28, rect.Width - 8, 16);
                g.DrawString($"DMG {effectiveDamage} | CD {effectiveMaxCd}", statFont, goldBrush, statsRect, centerSf);

                int descTop = rect.Y + 46;
                if (card.AppliedModifiers.Count > 0)
                {
                    Rectangle modRect = new Rectangle(rect.X + 4, descTop, rect.Width - 8, 14);
                    using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(alpha, 128, 232, 166)))
                    {
                        g.DrawString(string.Join(" ", card.AppliedModifiers), descFont, greenBrush, modRect, centerSf);
                    }
                    descTop += 14;
                }

                Rectangle descRect = new Rectangle(rect.X + 4, descTop, rect.Width - 8, (rect.Bottom - 20) - descTop);
                g.DrawString(def.Description, descFont, textBrush, descRect);

                Rectangle footerRect = new Rectangle(rect.X + 4, rect.Bottom - 19, rect.Width - 8, 15);
                string footerText = isExecutingNow ? "ACTIVE AIM" : "DRAG TO SWAP";
                g.DrawString(footerText, statFont, isExecutingNow ? goldBrush : textBrush, footerRect, centerSf);
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
                items.Add("Wait -1 CD");
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

        private void DrawStatPill(Graphics g, Rectangle rect, string text, Color textColor)
        {
            DrawRoundedBox(g, rect, 5, Color.FromArgb(24, 30, 41), Color.FromArgb(52, 64, 82), 1f);
            using (Font pillFont = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(textColor))
            using (StringFormat sf = CreateCenterFormat())
            {
                g.DrawString(text, pillFont, brush, rect, sf);
            }
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