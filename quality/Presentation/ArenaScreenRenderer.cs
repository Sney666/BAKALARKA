using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using RogueCards.Application;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public sealed class ArenaScreenRenderer : IScreenRenderer
    {
        public const int GridOriginX = 373;
        public const int GridOriginY = 74;
        public const int TileSize = 82;
        public static Rectangle GetTileRect(GridPos pos)
        {
            return new Rectangle(
                GridOriginX + (pos.X * TileSize),
                GridOriginY + (pos.Y * TileSize),
                TileSize,
                TileSize);
        }

        public static GridPos? ScreenToGrid(Point point)
        {
            int relX = point.X - GridOriginX;
            int relY = point.Y - GridOriginY;
            if (relX < 0 || relY < 0)
            {
                return null;
            }

            int gx = relX / TileSize;
            int gy = relY / TileSize;
            GridPos pos = new GridPos(gx, gy);
            return pos.IsValid() ? pos : null;
        }

        public void Render(Graphics g, UiRenderState state)
        {
            UiPrimitives.DrawScreenBackground(g);

            ActiveRunState? run = state.Engine.CurrentRun;
            if (run == null)
            {
                return;
            }

            DrawTopHud(g, state, run);
            DrawLeftEquippedAndActions(g, state, run);
            DrawCenterGrid(g, state, run);
            DrawRightCombatLogAndLegend(g, state, run);
            DrawBottomHandPanel(g, state, run);
            DrawDraggedCardGhost(g, state, run);

            if (run.PendingReward != null)
            {
                DrawRewardOverlay(g, state, run, run.PendingReward);
            }

            UiPrimitives.DrawAchievementToast(g, state.Engine.Achievements.ActiveToast);
        }

        private static void DrawTopHud(Graphics g, UiRenderState state, ActiveRunState run)
        {
            using SolidBrush barBrush = new SolidBrush(UiTheme.HeaderBg);
            using Pen borderPen = new Pen(UiTheme.BorderSubtle, 1f);
            g.FillRectangle(barBrush, 0, 0, UiTheme.WindowWidth, 64);
            g.DrawLine(borderPen, 0, 64, UiTheme.WindowWidth, 64);

            CharacterDefinition hero = state.Engine.GetCurrentRunCharacter();
            UiPrimitives.DrawStatPill(g, new Rectangle(18, 9, 178, 46), "HERO", hero.Name, UiTheme.ShieldCyan);
            UiPrimitives.DrawStatPill(
                g,
                new Rectangle(208, 9, 132, 46),
                "HEALTH",
                $"{run.Player.CurrentHp} / {run.Player.MaxHp}",
                run.Player.CurrentHp <= 2 ? UiTheme.HpBarFill : UiTheme.PoisonGreen);
            UiPrimitives.DrawStatPill(
                g,
                new Rectangle(352, 9, 136, 46),
                "WAVE",
                $"{run.CurrentWave} / {state.Engine.Database.Waves.Count}",
                UiTheme.AccentGold);
            UiPrimitives.DrawStatPill(g, new Rectangle(500, 9, 108, 46), "TURN", run.TurnNumber.ToString(), UiTheme.TextPrimary);
            UiPrimitives.DrawStatPill(
                g,
                new Rectangle(620, 9, 134, 46),
                "COINS",
                $"{state.Engine.Profile.Coins} c",
                UiTheme.AccentGold,
                UiTheme.BorderGold);

            if (state.Engine.CurrentAimingCard != null)
            {
                Rectangle aimBanner = new Rectangle(768, 12, 312, 40);
                UiPrimitives.FillRoundedRect(g, aimBanner, 8, Color.FromArgb(62, 52, 24), UiTheme.BorderGold, 1.8f);
                using SolidBrush aimBrush = new SolidBrush(UiTheme.AccentGold);
                g.DrawString(
                    $"AIMING: {state.Engine.CurrentAimingCard.Definition.Name} ({state.Engine.CurrentAimingCard.Definition.Targeting})",
                    UiTheme.FontSmallBold,
                    aimBrush,
                    aimBanner,
                    UiTheme.CenterFormat);
            }

            UiPrimitives.DrawButton(
                g,
                state,
                new Rectangle(1096, 12, 166, 40),
                "Save & Exit",
                "SaveAndExit",
                accentTop: UiTheme.AccentSlateTop,
                accentBottom: UiTheme.AccentSlateBottom);
        }

        private static void DrawLeftEquippedAndActions(Graphics g, UiRenderState state, ActiveRunState run)
        {
            Rectangle panel = new Rectangle(18, 72, 336, 414);
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);

            using SolidBrush headerBrush = new SolidBrush(UiTheme.AccentGold);
            using SolidBrush subBrush = new SolidBrush(UiTheme.TextMuted);
            g.DrawString(
                $"EQUIPPED QUEUE ({run.Player.EquippedQueue.Count}/3)",
                UiTheme.FontSection,
                headerBrush,
                panel.X + 14,
                panel.Y + 10);
            g.DrawString("Drag slots to reorder (0 turns)", UiTheme.FontBadge, subBrush, panel.X + 14, panel.Y + 32);

            bool aiming = state.Engine.CurrentAimingCard != null;
            int slotY = panel.Y + 52;
            for (int i = 0; i < 3; i++)
            {
                Rectangle slotRect = new Rectangle(panel.X + 14, slotY, panel.Width - 28, 72);
                DrawEquippedSlot(g, state, run, i, slotRect, aiming);
                slotY += 80;
            }

            DrawActionButtons(g, state, run, panel, aiming);
        }

        private static void DrawEquippedSlot(
            Graphics g,
            UiRenderState state,
            ActiveRunState run,
            int slotIndex,
            Rectangle rect,
            bool aiming)
        {
            bool hasCard = slotIndex < run.Player.EquippedQueue.Count;
            bool isExecuting = state.Engine.CurrentExecutingQueueIndex == slotIndex;
            bool isDragged = state.DraggingEquippedIndex == slotIndex;
            bool isDropHighlight = state.DraggingEquippedIndex >= 0
                && state.DraggingEquippedIndex != slotIndex
                && hasCard
                && state.IsHovered(rect);

            if (!hasCard)
            {
                UiPrimitives.FillRoundedRect(g, rect, 8, Color.FromArgb(20, 25, 36), UiTheme.BorderSubtle, 1f);
                using SolidBrush emptyBrush = new SolidBrush(UiTheme.TextMuted);
                g.DrawString($"Slot {slotIndex + 1} — Empty", UiTheme.FontSmall, emptyBrush, rect, UiTheme.CenterFormat);
                return;
            }

            CardInstance card = run.Player.EquippedQueue[slotIndex];
            Color catColor = UiTheme.GetCategoryColor(card.Definition.Category);
            Color fill = isDragged ? UiTheme.CardDimBg : UiTheme.CardBg;
            Color border = isExecuting
                ? UiTheme.AccentGold
                : (isDropHighlight ? UiTheme.ShieldCyan : UiTheme.BorderSubtle);

            UiPrimitives.FillRoundedRect(g, rect, 8, fill, border, isExecuting || isDropHighlight ? 2.4f : 1.3f);
            UiPrimitives.FillRoundedRect(g, new Rectangle(rect.X + 2, rect.Y + 2, 8, rect.Height - 4), 3, catColor);

            int dmg = state.Engine.GetEffectiveCardDamage(card);
            int maxCd = state.Engine.GetEffectiveCardMaxCooldown(card);

            using SolidBrush titleBrush = new SolidBrush(UiTheme.TextPrimary);
            using SolidBrush metaBrush = new SolidBrush(UiTheme.TextSecondary);
            using SolidBrush numBrush = new SolidBrush(UiTheme.AccentGold);

            g.DrawString($"{slotIndex + 1}. {card.Definition.Name}", UiTheme.FontBodyBold, titleBrush, rect.X + 16, rect.Y + 8);
            g.DrawString(
                $"DMG {dmg}  |  CD {maxCd}  |  {card.Definition.Targeting}",
                UiTheme.FontBadge,
                metaBrush,
                rect.X + 16,
                rect.Y + 30);

            string modLine = card.AppliedModifiers.Count > 0 ? string.Join(", ", card.AppliedModifiers) : card.Definition.Category.ToString();
            g.DrawString(modLine, UiTheme.FontBadge, card.HasPoison ? Brushes.LightGreen : numBrush, rect.X + 16, rect.Y + 48);

            if (isExecuting)
            {
                g.DrawString(
                    "ACTIVE",
                    UiTheme.FontBadge,
                    numBrush,
                    new Rectangle(rect.X, rect.Y + 8, rect.Width - 10, 18),
                    UiTheme.RightCenterFormat);
            }

            state.RegisterHit("EquippedSlot", rect, index: slotIndex, enabled: !aiming);
        }

        private static void DrawActionButtons(Graphics g, UiRenderState state, ActiveRunState run, Rectangle panel, bool aiming)
        {
            Rectangle playRect = new Rectangle(panel.X + 14, panel.Y + 298, panel.Width - 28, 42);
            if (aiming && state.Engine.CanCancelAiming)
            {
                UiPrimitives.DrawButton(
                    g,
                    state,
                    playRect,
                    "Cancel Aiming (ESC)",
                    "CancelAim",
                    accentTop: UiTheme.AccentRedTop,
                    accentBottom: UiTheme.AccentRedBottom);
            }
            else
            {
                UiPrimitives.DrawButton(
                    g,
                    state,
                    playRect,
                    "PLAY EQUIPPED (1 Turn)",
                    "PlayQueue",
                    enabled: !aiming && run.Player.EquippedQueue.Count > 0,
                    accentTop: UiTheme.AccentGreenTop,
                    accentBottom: UiTheme.AccentGreenBottom);
            }

            int halfW = (panel.Width - 38) / 2;
            Rectangle waitRect = new Rectangle(panel.X + 14, panel.Y + 348, halfW, 38);
            Rectangle specRect = new Rectangle(waitRect.Right + 10, panel.Y + 348, halfW, 38);

            UiPrimitives.DrawButton(
                g,
                state,
                waitRect,
                "Wait (Space)",
                "Wait",
                enabled: !aiming,
                accentTop: UiTheme.AccentSlateTop,
                accentBottom: UiTheme.AccentSlateBottom);

            CharacterDefinition hero = state.Engine.GetCurrentRunCharacter();
            string specLabel = run.Player.IsSpecialReady
                ? $"{hero.SpecialName}"
                : $"Special (CD {run.Player.SpecialCooldown})";

            UiPrimitives.DrawButton(
                g,
                state,
                specRect,
                specLabel,
                "Special",
                enabled: !aiming && run.Player.IsSpecialReady,
                accentTop: UiTheme.AccentPurpleTop,
                accentBottom: UiTheme.AccentPurpleBottom,
                font: UiTheme.FontSmallBold);

            using SolidBrush hintBrush = new SolidBrush(UiTheme.TextMuted);
            g.DrawString(
                "Move: WASD / Arrows / Click adjacent tile  |  Play: Enter",
                UiTheme.FontBadge,
                hintBrush,
                new Rectangle(panel.X + 10, panel.Bottom - 22, panel.Width - 20, 18),
                UiTheme.CenterFormat);
        }

        private static void DrawCenterGrid(Graphics g, UiRenderState state, ActiveRunState run)
        {
            Rectangle frame = new Rectangle(GridOriginX - 6, GridOriginY - 6, (TileSize * GridPos.GridSize) + 12, (TileSize * GridPos.GridSize) + 12);
            UiPrimitives.FillRoundedRect(g, frame, 10, Color.FromArgb(18, 23, 34), UiTheme.BorderSubtle, 2f);

            bool aiming = state.Engine.CurrentAimingCard != null;

            for (int y = 0; y < GridPos.GridSize; y++)
            {
                for (int x = 0; x < GridPos.GridSize; x++)
                {
                    GridPos pos = new GridPos(x, y);
                    Rectangle tileRect = GetTileRect(pos);
                    DrawBaseTile(g, state, run, pos, tileRect, aiming);
                    state.RegisterHit("GridTile", tileRect, index: (y * GridPos.GridSize) + x);
                }
            }

            foreach (GroundHazard hazard in run.Hazards)
            {
                EntitySpritePainter.DrawHazard(g, GetTileRect(hazard.Position), hazard, state.PulsePhase);
            }

            DrawEnemyTelegraphs(g, state, run);
            DrawAimPreviewOverlay(g, state);

            CharacterDefinition hero = state.Engine.GetCurrentRunCharacter();
            EntitySpritePainter.DrawPlayer(g, GetTileRect(run.Player.Position), run.Player, hero, state.PulsePhase);

            foreach (EnemyInstance enemy in run.Enemies)
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }

                EnemyDefinition def = state.Engine.Database.Enemies[enemy.Type];
                EntitySpritePainter.DrawEnemy(g, GetTileRect(enemy.Position), enemy, def, state.PulsePhase);
            }
        }

        private static void DrawBaseTile(
            Graphics g,
            UiRenderState state,
            ActiveRunState run,
            GridPos pos,
            Rectangle tileRect,
            bool aiming)
        {
            bool checker = (pos.X + pos.Y) % 2 == 0;
            Color baseColor = checker ? Color.FromArgb(30, 38, 54) : Color.FromArgb(25, 32, 46);

            bool hovered = state.HoveredGridTile.HasValue && state.HoveredGridTile.Value == pos;
            bool isWalkableAdjacent = !aiming
                && pos.ManhattanDistance(run.Player.Position) == 1
                && !run.IsTileOccupied(pos);

            if (hovered && isWalkableAdjacent)
            {
                baseColor = Color.FromArgb(42, 74, 114);
            }

            using SolidBrush fill = new SolidBrush(baseColor);
            using Pen gridPen = new Pen(isWalkableAdjacent && hovered ? UiTheme.ShieldCyan : Color.FromArgb(46, 58, 80), 1f);
            g.FillRectangle(fill, tileRect);
            g.DrawRectangle(gridPen, tileRect);
        }

        private static void DrawEnemyTelegraphs(Graphics g, UiRenderState state, ActiveRunState run)
        {
            float pulse = 0.5f + (0.5f * (float)Math.Sin(state.PulsePhase * 5.0f));
            int fillAlpha = 48 + (int)(56 * pulse);
            int borderAlpha = 145 + (int)(95 * pulse);

            using SolidBrush dangerBrush = new SolidBrush(Color.FromArgb(fillAlpha, 226, 46, 56));
            using Pen dangerPen = new Pen(Color.FromArgb(borderAlpha, 255, 86, 92), 2.2f);
            using Pen dirPen = new Pen(Color.FromArgb(215, 255, 196, 128), 2f);

            foreach (EnemyInstance enemy in run.Enemies)
            {
                if (!enemy.IsAlive || enemy.State != EnemyState.Charging)
                {
                    continue;
                }

                foreach (GridPos tile in enemy.LockedTargetTiles)
                {
                    Rectangle r = GetTileRect(tile);
                    Rectangle inset = new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
                    g.FillRectangle(dangerBrush, inset);
                    g.DrawRectangle(dangerPen, inset);
                    DrawDirectionChevron(g, dirPen, inset, enemy.LockedDirection);
                }
            }
        }

        private static void DrawDirectionChevron(Graphics g, Pen pen, Rectangle rect, GridPos dir)
        {
            if (dir.X == 0 && dir.Y == 0)
            {
                return;
            }

            int cx = rect.X + (rect.Width / 2);
            int cy = rect.Y + (rect.Height / 2);
            int tipX = cx + (dir.X * 12);
            int tipY = cy + (dir.Y * 12);
            int baseX = cx - (dir.X * 6);
            int baseY = cy - (dir.Y * 6);

            g.DrawLine(pen, baseX, baseY, tipX, tipY);
        }

        private static void DrawAimPreviewOverlay(Graphics g, UiRenderState state)
        {
            AimPreviewResult? preview = state.CurrentAimPreview;
            if (state.Engine.CurrentAimingCard == null || preview == null || !preview.IsValid)
            {
                return;
            }

            using SolidBrush affectBrush = new SolidBrush(Color.FromArgb(72, 74, 186, 244));
            using Pen affectPen = new Pen(UiTheme.ShieldCyan, 2.2f);
            using SolidBrush impactBrush = new SolidBrush(Color.FromArgb(104, 246, 194, 72));
            using Pen impactPen = new Pen(UiTheme.AccentGold, 2.8f);

            foreach (GridPos tile in preview.AffectedTiles)
            {
                Rectangle r = GetTileRect(tile);
                Rectangle inset = new Rectangle(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8);
                g.FillRectangle(affectBrush, inset);
                g.DrawRectangle(affectPen, inset);
            }

            foreach (GridPos tile in preview.ImpactTiles)
            {
                Rectangle r = GetTileRect(tile);
                Rectangle inset = new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, r.Height - 12);
                g.FillRectangle(impactBrush, inset);
                g.DrawRectangle(impactPen, inset);
            }

            if (preview.MoveDestination.HasValue)
            {
                Rectangle dest = GetTileRect(preview.MoveDestination.Value);
                using Pen movePen = new Pen(UiTheme.PoisonGreen, 2.6f);
                g.DrawEllipse(movePen, dest.X + 14, dest.Y + 14, dest.Width - 28, dest.Height - 28);
            }
        }

        private static void DrawRightCombatLogAndLegend(Graphics g, UiRenderState state, ActiveRunState run)
        {
            Rectangle logPanel = new Rectangle(806, 72, 456, 276);
            UiPrimitives.FillRoundedRect(g, logPanel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);

            using SolidBrush headerBrush = new SolidBrush(UiTheme.AccentGold);
            g.DrawString("COMBAT LOG", UiTheme.FontSection, headerBrush, logPanel.X + 14, logPanel.Y + 10);

            int y = logPanel.Y + 36;
            List<string> entries = run.RecentLogEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                string line = entries[i];
                Color color = ResolveLogColor(line);
                using SolidBrush lineBrush = new SolidBrush(color);
                Rectangle lineRect = new Rectangle(logPanel.X + 14, y, logPanel.Width - 28, 28);
                g.DrawString(line, UiTheme.FontSmall, lineBrush, lineRect, UiTheme.TopLeftWrapFormat);
                y += 29;
            }

            DrawTacticalInspectorPanel(g, state, run, new Rectangle(806, 358, 456, 128));
        }

        private static void DrawTacticalInspectorPanel(Graphics g, UiRenderState state, ActiveRunState run, Rectangle panel)
        {
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);

            using SolidBrush titleBrush = new SolidBrush(UiTheme.AccentGold);
            using SolidBrush bodyBrush = new SolidBrush(UiTheme.TextSecondary);

            if (state.Engine.CurrentAimingCard != null)
            {
                CardInstance aimingCard = state.Engine.CurrentAimingCard;
                g.DrawString($"AIMING: {aimingCard.Definition.Name}", UiTheme.FontBodyBold, titleBrush, panel.X + 14, panel.Y + 10);
                string summary = state.CurrentAimPreview != null && state.CurrentAimPreview.IsValid
                    ? state.CurrentAimPreview.Summary
                    : $"Hover a tile or press direction keys ({aimingCard.Definition.Targeting}).";
                g.DrawString(summary, UiTheme.FontBody, bodyBrush, new Rectangle(panel.X + 14, panel.Y + 34, panel.Width - 28, 82), UiTheme.TopLeftWrapFormat);
                return;
            }

            if (state.HoveredGridTile.HasValue)
            {
                EnemyInstance? hoveredEnemy = run.GetEnemyAt(state.HoveredGridTile.Value);
                if (hoveredEnemy != null && state.Engine.Database.Enemies.TryGetValue(hoveredEnemy.Type, out EnemyDefinition? def))
                {
                    g.DrawString(
                        $"INSPECT: {def.Name} ({hoveredEnemy.CurrentHp}/{hoveredEnemy.MaxHp} HP | {def.Damage} DMG | +{def.CoinReward}c)",
                        UiTheme.FontSmallBold,
                        titleBrush,
                        panel.X + 14,
                        panel.Y + 10);
                    g.DrawString(def.Description, UiTheme.FontSmall, bodyBrush, new Rectangle(panel.X + 14, panel.Y + 34, panel.Width - 28, 82), UiTheme.TopLeftWrapFormat);
                    return;
                }
            }

            g.DrawString("TACTICAL LEGEND", UiTheme.FontSmallBold, titleBrush, panel.X + 14, panel.Y + 10);
            DrawLegendItem(g, panel.X + 14, panel.Y + 38, UiTheme.MeleeHeader, "Red Pulse: Locked enemy attack (hits enemies too — Friendly Fire!)");
            DrawLegendItem(g, panel.X + 14, panel.Y + 60, UiTheme.ShieldCyan, "Cyan / Gold: Dynamic aiming preview and confirmed target impacts");
            DrawLegendItem(g, panel.X + 14, panel.Y + 82, UiTheme.PoisonGreen, "Green Pool: Toxic poison ground (1 dmg/turn for 3 turns)");
            DrawLegendItem(g, panel.X + 14, panel.Y + 104, UiTheme.AccentGold, "Steel Jaws: Armed Bear Trap (2 dmg + stun + cancels enemy charge)");
        }

        private static void DrawLegendItem(Graphics g, int x, int y, Color swatch, string text)
        {
            using SolidBrush swatchBrush = new SolidBrush(swatch);
            using SolidBrush textBrush = new SolidBrush(UiTheme.TextSecondary);
            g.FillRectangle(swatchBrush, x, y + 3, 12, 12);
            g.DrawString(text, UiTheme.FontSmall, textBrush, x + 18, y);
        }

        private static void DrawBottomHandPanel(Graphics g, UiRenderState state, ActiveRunState run)
        {
            Rectangle panel = new Rectangle(18, 496, 1244, 210);
            UiPrimitives.FillRoundedRect(g, panel, 10, UiTheme.PanelBg, UiTheme.BorderSubtle);

            using SolidBrush headerBrush = new SolidBrush(UiTheme.AccentGold);
            g.DrawString(
                $"HAND ({run.Player.Hand.Count}) — Click a ready card to equip into the queue (1 turn)",
                UiTheme.FontSmallBold,
                headerBrush,
                panel.X + 14,
                panel.Y + 8);

            if (run.Player.Hand.Count == 0)
            {
                using SolidBrush emptyBrush = new SolidBrush(UiTheme.TextMuted);
                g.DrawString("All cards are currently in your Equipped Queue.", UiTheme.FontBody, emptyBrush, panel, UiTheme.CenterFormat);
                return;
            }

            bool aiming = state.Engine.CurrentAimingCard != null;
            bool queueFull = run.Player.EquippedQueue.Count >= 3;
            int count = run.Player.Hand.Count;
            int gap = 12;
            int availableW = panel.Width - 28;
            int cardW = Math.Min(196, (availableW - ((count - 1) * gap)) / count);
            int cardH = 166;
            int startX = panel.X + 14;
            int cardY = panel.Y + 32;

            for (int i = 0; i < count; i++)
            {
                CardInstance card = run.Player.Hand[i];
                Rectangle cardRect = new Rectangle(startX + (i * (cardW + gap)), cardY, cardW, cardH);
                bool canEquip = !aiming && !queueFull && card.IsReady;

                string? footer = !card.IsReady
                    ? $"COOLDOWN: {card.CurrentCooldown}"
                    : (queueFull ? "QUEUE FULL" : "CLICK TO EQUIP");

                UiPrimitives.DrawCardFace(
                    g,
                    cardRect,
                    card.Definition,
                    state.Engine.GetEffectiveCardDamage(card),
                    state.Engine.GetEffectiveCardMaxCooldown(card),
                    card.CurrentCooldown,
                    card.AppliedModifiers,
                    card.HasPoison,
                    canEquip && state.IsHovered(cardRect),
                    false,
                    false,
                    !card.IsReady,
                    footer);

                state.RegisterHit("EquipCard", cardRect, index: i, enabled: canEquip);
            }
        }

        private static void DrawDraggedCardGhost(Graphics g, UiRenderState state, ActiveRunState run)
        {
            int idx = state.DraggingEquippedIndex;
            if (idx < 0 || idx >= run.Player.EquippedQueue.Count)
            {
                return;
            }

            CardInstance card = run.Player.EquippedQueue[idx];
            Rectangle ghost = new Rectangle(state.DragCurrentPoint.X - 140, state.DragCurrentPoint.Y - 30, 280, 60);
            UiPrimitives.FillRoundedRect(g, ghost, 8, Color.FromArgb(220, 36, 48, 72), UiTheme.BorderGold, 2f);
            using SolidBrush brush = new SolidBrush(UiTheme.TextPrimary);
            g.DrawString($"Moving: {card.Definition.Name}", UiTheme.FontBodyBold, brush, ghost, UiTheme.CenterFormat);
        }

        private static void DrawRewardOverlay(
            Graphics g,
            UiRenderState state,
            ActiveRunState run,
            PendingRewardState reward)
        {
            using SolidBrush scrim = new SolidBrush(Color.FromArgb(204, 10, 12, 18));
            g.FillRectangle(scrim, 0, 0, UiTheme.WindowWidth, UiTheme.WindowHeight);
            state.RegisterHit("ModalBlocker", new Rectangle(0, 0, UiTheme.WindowWidth, UiTheme.WindowHeight));

            Rectangle modal = new Rectangle(110, 74, 1060, 576);
            UiPrimitives.DrawGradientPanel(g, modal, 14, Color.FromArgb(30, 38, 56), Color.FromArgb(20, 25, 38), UiTheme.BorderGold, 2.2f);

            if (reward.Type == RewardType.StartingDraft)
            {
                DrawStartingDraftModal(g, state, run, reward, modal);
            }
            else if (reward.Type == RewardType.PickCard)
            {
                DrawPickCardModal(g, state, run, reward, modal);
            }
            else if (reward.Type == RewardType.Upgrade)
            {
                DrawUpgradeModal(g, state, run, reward, modal);
            }
        }

        private static void DrawStartingDraftModal(
            Graphics g,
            UiRenderState state,
            ActiveRunState run,
            PendingRewardState reward,
            Rectangle modal)
        {
            DrawModalHeader(
                g,
                modal,
                $"STARTING DRAFT — PICK 2 CARDS ({reward.SelectedStartingWeaponIds.Count} / 2)",
                "Choose two weapons from your unlocked armory to begin Level 1.");

            int cardW = 232;
            int cardH = 264;
            int gap = 20;
            int totalW = (reward.OfferedWeaponIds.Count * cardW) + ((reward.OfferedWeaponIds.Count - 1) * gap);
            int startX = modal.X + ((modal.Width - totalW) / 2);

            for (int i = 0; i < reward.OfferedWeaponIds.Count; i++)
            {
                string id = reward.OfferedWeaponIds[i];
                WeaponDefinition weapon = state.Engine.Database.Weapons[id];
                bool selected = reward.SelectedStartingWeaponIds.Contains(id);
                Rectangle rect = new Rectangle(startX + (i * (cardW + gap)), modal.Y + 128, cardW, cardH);

                UiPrimitives.DrawCardFace(
                    g,
                    rect,
                    weapon,
                    weapon.BaseDamage,
                    weapon.BaseCooldown,
                    0,
                    null,
                    false,
                    !selected && state.IsHovered(rect),
                    selected,
                    false,
                    selected,
                    selected ? "SELECTED" : "CLICK TO PICK");

                state.RegisterHit("PickRewardCard", rect, payload: weapon.Id, enabled: !selected);
            }

            DrawRewardFooterButtons(g, state, run, reward, modal);
        }

        private static void DrawPickCardModal(
            Graphics g,
            UiRenderState state,
            ActiveRunState run,
            PendingRewardState reward,
            Rectangle modal)
        {
            DrawModalHeader(
                g,
                modal,
                "WAVE REWARD — PICK A NEW CARD",
                "All card and Special cooldowns have been reset to 0! Pick one card to add to your hand.");

            int cardW = 300;
            int cardH = 270;
            int gap = 40;
            int totalW = (reward.OfferedWeaponIds.Count * cardW) + (Math.Max(0, reward.OfferedWeaponIds.Count - 1) * gap);
            int startX = modal.X + ((modal.Width - totalW) / 2);

            for (int i = 0; i < reward.OfferedWeaponIds.Count; i++)
            {
                WeaponDefinition weapon = state.Engine.Database.Weapons[reward.OfferedWeaponIds[i]];
                Rectangle rect = new Rectangle(startX + (i * (cardW + gap)), modal.Y + 128, cardW, cardH);

                UiPrimitives.DrawCardFace(
                    g,
                    rect,
                    weapon,
                    weapon.BaseDamage,
                    weapon.BaseCooldown,
                    0,
                    null,
                    false,
                    state.IsHovered(rect),
                    false,
                    false,
                    false,
                    $"Range: {weapon.Range} | {weapon.Targeting}");

                state.RegisterHit("PickRewardCard", rect, payload: weapon.Id);
            }

            DrawRewardFooterButtons(g, state, run, reward, modal);
        }

        private static void DrawUpgradeModal(
            Graphics g,
            UiRenderState state,
            ActiveRunState run,
            PendingRewardState reward,
            Rectangle modal)
        {
            DrawModalHeader(
                g,
                modal,
                "WAVE REWARD — UPGRADE A CARD",
                "All cooldowns reset! Step 1: Select a modifier below. Step 2: Click one of your cards to apply it.");

            int modW = 310;
            int modH = 88;
            int gap = 18;
            int modStartX = modal.X + ((modal.Width - ((reward.OfferedUpgrades.Count * modW) + ((reward.OfferedUpgrades.Count - 1) * gap))) / 2);

            for (int i = 0; i < reward.OfferedUpgrades.Count; i++)
            {
                UpgradeModifierType mod = reward.OfferedUpgrades[i];
                Rectangle rect = new Rectangle(modStartX + (i * (modW + gap)), modal.Y + 104, modW, modH);
                bool selected = state.SelectedUpgradeModifier == mod;
                bool hovered = state.IsHovered(rect);

                Color fill = selected ? Color.FromArgb(52, 72, 108) : UiTheme.CardBg;
                Color border = selected ? UiTheme.BorderGold : (hovered ? UiTheme.BorderHighlight : UiTheme.BorderSubtle);
                UiPrimitives.FillRoundedRect(g, rect, 8, fill, border, selected ? 2.4f : 1.4f);

                using SolidBrush titleBrush = new SolidBrush(selected ? UiTheme.AccentGold : UiTheme.TextPrimary);
                using SolidBrush descBrush = new SolidBrush(UiTheme.TextSecondary);
                g.DrawString(state.Engine.Rewards.GetModifierLabel(mod), UiTheme.FontCardTitle, titleBrush, rect.X + 12, rect.Y + 10);
                g.DrawString(
                    state.Engine.Rewards.GetModifierDescription(mod),
                    UiTheme.FontSmall,
                    descBrush,
                    new Rectangle(rect.X + 12, rect.Y + 34, rect.Width - 24, 46),
                    UiTheme.TopLeftWrapFormat);

                state.RegisterHit("SelectUpgradeMod", rect, payload: mod.ToString());
            }

            List<CardInstance> ownedCards = run.Player.GetAllOwnedCards().ToList();
            int cardCount = ownedCards.Count;
            int cardGap = 12;
            int cardW = Math.Min(196, (modal.Width - 60 - ((cardCount - 1) * cardGap)) / Math.Max(1, cardCount));
            int cardH = 196;
            int totalCardsW = (cardCount * cardW) + ((cardCount - 1) * cardGap);
            int cardsStartX = modal.X + ((modal.Width - totalCardsW) / 2);

            for (int i = 0; i < cardCount; i++)
            {
                CardInstance card = ownedCards[i];
                Rectangle rect = new Rectangle(cardsStartX + (i * (cardW + cardGap)), modal.Y + 224, cardW, cardH);
                bool readyToApply = state.SelectedUpgradeModifier.HasValue;

                UiPrimitives.DrawCardFace(
                    g,
                    rect,
                    card.Definition,
                    state.Engine.GetEffectiveCardDamage(card),
                    state.Engine.GetEffectiveCardMaxCooldown(card),
                    0,
                    card.AppliedModifiers,
                    card.HasPoison,
                    readyToApply && state.IsHovered(rect),
                    false,
                    false,
                    !readyToApply,
                    readyToApply ? "CLICK TO UPGRADE" : "SELECT MODIFIER FIRST");

                state.RegisterHit("ApplyUpgradeToCard", rect, index: card.InstanceId, enabled: readyToApply);
            }

            DrawRewardFooterButtons(g, state, run, reward, modal);
        }

        private static void DrawModalHeader(Graphics g, Rectangle modal, string title, string subtitle)
        {
            using SolidBrush titleBrush = new SolidBrush(UiTheme.AccentGold);
            using SolidBrush subBrush = new SolidBrush(UiTheme.TextSecondary);

            g.DrawString(title, UiTheme.FontTitle, titleBrush, new Rectangle(modal.X + 30, modal.Y + 20, modal.Width - 60, 38), UiTheme.CenterFormat);
            g.DrawString(subtitle, UiTheme.FontBody, subBrush, new Rectangle(modal.X + 40, modal.Y + 60, modal.Width - 80, 26), UiTheme.CenterFormat);
        }

        private static void DrawRewardFooterButtons(
            Graphics g,
            UiRenderState state,
            ActiveRunState run,
            PendingRewardState reward,
            Rectangle modal)
        {
            int cost = reward.GetNextRerollCost(run.Player.FreeRerollsRemaining);
            bool canReroll = cost == 0 || state.Engine.Profile.Coins >= cost;
            string rerollLabel = cost == 0
                ? $"Reroll (FREE — Charisma)"
                : $"Reroll ({cost} Coins)";

            int y = modal.Bottom - 70;
            if (reward.CanSkip)
            {
                UiPrimitives.DrawButton(
                    g,
                    state,
                    new Rectangle(modal.X + 270, y, 250, 44),
                    rerollLabel,
                    "RerollReward",
                    enabled: canReroll,
                    accentTop: UiTheme.AccentBlueTop,
                    accentBottom: UiTheme.AccentBlueBottom);

                UiPrimitives.DrawButton(
                    g,
                    state,
                    new Rectangle(modal.X + 540, y, 250, 44),
                    "Skip Reward",
                    "SkipReward",
                    accentTop: UiTheme.AccentSlateTop,
                    accentBottom: UiTheme.AccentSlateBottom);
            }
            else
            {
                UiPrimitives.DrawButton(
                    g,
                    state,
                    new Rectangle(modal.X + 405, y, 250, 44),
                    rerollLabel,
                    "RerollReward",
                    enabled: canReroll,
                    accentTop: UiTheme.AccentBlueTop,
                    accentBottom: UiTheme.AccentBlueBottom);
            }
        }

        private static Color ResolveLogColor(string line)
        {
            if (line.Contains("Wave ", StringComparison.OrdinalIgnoreCase) || line.Contains("coins", StringComparison.OrdinalIgnoreCase))
            {
                return UiTheme.AccentGold;
            }

            if (line.Contains("Player took", StringComparison.OrdinalIgnoreCase) || line.Contains("Defeated by", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(248, 126, 132);
            }

            if (line.Contains("Poison", StringComparison.OrdinalIgnoreCase))
            {
                return UiTheme.PoisonGreen;
            }

            if (line.Contains("Friendly fire", StringComparison.OrdinalIgnoreCase))
            {
                return UiTheme.ShieldCyan;
            }

            return UiTheme.TextSecondary;
        }
    }
}