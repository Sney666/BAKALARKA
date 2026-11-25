using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public static class EntitySpritePainter
    {
        public static void DrawHazard(Graphics g, Rectangle tileRect, GroundHazard hazard, float pulsePhase)
        {
            if (hazard.Type == HazardType.PoisonPool)
            {
                DrawPoisonPool(g, tileRect, hazard, pulsePhase);
                return;
            }
            if (hazard.Type == HazardType.BearTrap)
            {
                DrawBearTrap(g, tileRect, hazard);
            }
        }

        public static void DrawPlayer(
            Graphics g,
            Rectangle tileRect,
            PlayerState player,
            CharacterDefinition character,
            float pulsePhase)
        {
            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2) + 2;

            if (player.IsImmuneThisTurn)
            {
                DrawImmunityAura(g, cx, cy, pulsePhase);
            }

            if (character.SpecialType == CharacterSpecialType.IronBulwark)
            {
                DrawShieldmaidenSprite(g, cx, cy);
            }
            else
            {
                DrawVikingSprite(g, cx, cy);
            }

            string badge = player.IsImmuneThisTurn
                ? "BULWARK"
                : (player.PoisonTurnsRemaining > 0 ? $"POISON {player.PoisonTurnsRemaining}T" : character.Name);
            Color badgeColor = player.IsImmuneThisTurn
                ? UiTheme.ShieldCyan
                : (player.PoisonTurnsRemaining > 0 ? UiTheme.PoisonGreen : UiTheme.AccentBlueTop);

            DrawTopBadge(g, tileRect, badge, badgeColor);
            DrawHpBar(g, tileRect, player.CurrentHp, player.MaxHp, player.PoisonTurnsRemaining > 0, isPlayer: true);
        }

        public static void DrawEnemy(
            Graphics g,
            Rectangle tileRect,
            EnemyInstance enemy,
            EnemyDefinition def,
            float pulsePhase)
        {
            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2) + 2;

            switch (enemy.Type)
            {
                case EnemyType.Pawn:
                    DrawPawnSprite(g, cx, cy);
                    break;
                case EnemyType.Archer:
                    DrawArcherSprite(g, cx, cy);
                    break;
                case EnemyType.Spearman:
                    DrawSpearmanSprite(g, cx, cy);
                    break;
                case EnemyType.Shaman:
                    DrawShamanSprite(g, cx, cy);
                    break;
                case EnemyType.VikingChief:
                    DrawVikingChiefSprite(g, cx, cy, pulsePhase);
                    break;
            }

            (string badgeText, Color badgeColor) = ResolveEnemyBadge(enemy, def);
            DrawTopBadge(g, tileRect, badgeText, badgeColor);
            DrawNameTag(g, tileRect, def.Name);
            DrawHpBar(g, tileRect, enemy.CurrentHp, enemy.MaxHp, enemy.PoisonTurnsRemaining > 0, isPlayer: false);
        }

        private static void DrawVikingSprite(Graphics g, int cx, int cy)
        {
            using SolidBrush cloakBrush = new SolidBrush(Color.FromArgb(36, 74, 134));
            using SolidBrush armorBrush = new SolidBrush(Color.FromArgb(62, 114, 188));
            using SolidBrush goldBrush = new SolidBrush(Color.FromArgb(228, 186, 86));
            using SolidBrush skinBrush = new SolidBrush(Color.FromArgb(232, 186, 150));
            using SolidBrush beardBrush = new SolidBrush(Color.FromArgb(182, 108, 44));
            using SolidBrush steelBrush = new SolidBrush(Color.FromArgb(176, 190, 212));
            using SolidBrush hornBrush = new SolidBrush(Color.FromArgb(242, 232, 200));
            using Pen outline = new Pen(Color.FromArgb(20, 26, 38), 1.4f);

            Point[] cloak =
            {
            new Point(cx - 16, cy + 16),
            new Point(cx - 11, cy - 2),
            new Point(cx + 11, cy - 2),
            new Point(cx + 16, cy + 16)
        };
            g.FillPolygon(cloakBrush, cloak);

            g.FillRectangle(armorBrush, cx - 10, cy - 2, 20, 17);
            g.FillRectangle(goldBrush, cx - 10, cy + 5, 20, 4);

            g.FillEllipse(skinBrush, cx - 8, cy - 15, 16, 14);
            Point[] beard =
            {
            new Point(cx - 8, cy - 7),
            new Point(cx, cy + 3),
            new Point(cx + 8, cy - 7)
        };
            g.FillPolygon(beardBrush, beard);

            g.FillPie(steelBrush, cx - 10, cy - 22, 20, 16, 180, 180);
            g.DrawPie(outline, cx - 10, cy - 22, 20, 16, 180, 180);
            g.FillRectangle(goldBrush, cx - 10, cy - 15, 20, 3);

            Point[] leftHorn = { new Point(cx - 9, cy - 16), new Point(cx - 17, cy - 24), new Point(cx - 10, cy - 20) };
            Point[] rightHorn = { new Point(cx + 9, cy - 16), new Point(cx + 17, cy - 24), new Point(cx + 10, cy - 20) };
            g.FillPolygon(hornBrush, leftHorn);
            g.FillPolygon(hornBrush, rightHorn);

            using Pen axeShaft = new Pen(Color.FromArgb(132, 86, 46), 2.5f);
            g.DrawLine(axeShaft, cx + 13, cy + 14, cx + 16, cy - 10);
            Point[] axeHead = { new Point(cx + 15, cy - 10), new Point(cx + 23, cy - 6), new Point(cx + 21, cy + 2), new Point(cx + 14, cy - 2) };
            g.FillPolygon(steelBrush, axeHead);
        }

        private static void DrawShieldmaidenSprite(Graphics g, int cx, int cy)
        {
            using SolidBrush tunicBrush = new SolidBrush(Color.FromArgb(42, 128, 152));
            using SolidBrush silverBrush = new SolidBrush(Color.FromArgb(204, 218, 232));
            using SolidBrush cyanBrush = new SolidBrush(Color.FromArgb(92, 216, 242));
            using SolidBrush skinBrush = new SolidBrush(Color.FromArgb(236, 194, 164));
            using SolidBrush wingBrush = new SolidBrush(Color.FromArgb(236, 246, 255));
            using Pen shieldRim = new Pen(Color.FromArgb(212, 226, 242), 2.2f);
            using Pen runePen = new Pen(Color.FromArgb(92, 216, 242), 1.6f);

            g.FillRectangle(tunicBrush, cx - 9, cy - 2, 18, 17);
            g.FillRectangle(silverBrush, cx - 11, cy - 2, 22, 5);

            g.FillEllipse(skinBrush, cx - 7, cy - 15, 14, 13);
            g.FillPie(silverBrush, cx - 9, cy - 22, 18, 15, 180, 180);
            g.FillRectangle(cyanBrush, cx - 9, cy - 15, 18, 3);

            Point[] leftWing = { new Point(cx - 9, cy - 16), new Point(cx - 19, cy - 24), new Point(cx - 13, cy - 13) };
            Point[] rightWing = { new Point(cx + 9, cy - 16), new Point(cx + 19, cy - 24), new Point(cx + 13, cy - 13) };
            g.FillPolygon(wingBrush, leftWing);
            g.FillPolygon(wingBrush, rightWing);

            Rectangle shieldRect = new Rectangle(cx - 21, cy - 3, 18, 18);
            using SolidBrush shieldFace = new SolidBrush(Color.FromArgb(34, 72, 96));
            g.FillEllipse(shieldFace, shieldRect);
            g.DrawEllipse(shieldRim, shieldRect);
            g.DrawLine(runePen, cx - 12, cy, cx - 12, cy + 12);
            g.DrawLine(runePen, cx - 17, cy + 6, cx - 7, cy + 6);

            using Pen swordPen = new Pen(Color.FromArgb(218, 232, 246), 2.4f);
            g.DrawLine(swordPen, cx + 13, cy + 12, cx + 17, cy - 11);
        }

        private static void DrawPawnSprite(Graphics g, int cx, int cy)
        {
            using SolidBrush bodyBrush = new SolidBrush(Color.FromArgb(146, 54, 58));
            using SolidBrush helmBrush = new SolidBrush(Color.FromArgb(138, 148, 164));
            using SolidBrush skinBrush = new SolidBrush(Color.FromArgb(216, 172, 140));
            using SolidBrush woodBrush = new SolidBrush(Color.FromArgb(132, 88, 48));
            using SolidBrush steelBrush = new SolidBrush(Color.FromArgb(188, 198, 212));
            using Pen rimPen = new Pen(Color.FromArgb(90, 98, 112), 1.8f);

            g.FillRectangle(bodyBrush, cx - 9, cy - 1, 18, 15);
            g.FillEllipse(skinBrush, cx - 6, cy - 13, 12, 11);
            g.FillPie(helmBrush, cx - 8, cy - 19, 16, 13, 180, 180);

            Rectangle shield = new Rectangle(cx - 18, cy - 1, 14, 14);
            g.FillEllipse(woodBrush, shield);
            g.DrawEllipse(rimPen, shield);
            g.FillEllipse(steelBrush, cx - 13, cy + 4, 4, 4);

            using Pen handle = new Pen(Color.FromArgb(118, 76, 40), 2.2f);
            g.DrawLine(handle, cx + 11, cy + 11, cx + 15, cy - 8);
            Point[] blade = { new Point(cx + 14, cy - 8), new Point(cx + 21, cy - 5), new Point(cx + 19, cy + 1), new Point(cx + 13, cy - 2) };
            g.FillPolygon(steelBrush, blade);
        }

        private static void DrawArcherSprite(Graphics g, int cx, int cy)
        {
            using SolidBrush cloakBrush = new SolidBrush(Color.FromArgb(46, 108, 66));
            using SolidBrush hoodBrush = new SolidBrush(Color.FromArgb(34, 82, 50));
            using SolidBrush skinBrush = new SolidBrush(Color.FromArgb(222, 182, 150));
            using Pen bowPen = new Pen(Color.FromArgb(166, 114, 58), 2.4f);
            using Pen stringPen = new Pen(Color.FromArgb(220, 224, 232), 1.2f);
            using Pen arrowPen = new Pen(Color.FromArgb(236, 196, 98), 1.8f);

            Point[] cloak = { new Point(cx - 12, cy + 14), new Point(cx - 7, cy - 3), new Point(cx + 7, cy - 3), new Point(cx + 12, cy + 14) };
            g.FillPolygon(cloakBrush, cloak);
            g.FillEllipse(skinBrush, cx - 5, cy - 13, 10, 10);

            Point[] hood = { new Point(cx - 9, cy - 8), new Point(cx, cy - 21), new Point(cx + 9, cy - 8) };
            g.FillPolygon(hoodBrush, hood);

            g.DrawArc(bowPen, cx + 4, cy - 13, 14, 26, -85, 170);
            g.DrawLine(stringPen, cx + 11, cy - 12, cx + 11, cy + 12);
            g.DrawLine(arrowPen, cx + 6, cy, cx + 22, cy);
        }

        private static void DrawSpearmanSprite(Graphics g, int cx, int cy)
        {
            using SolidBrush armorBrush = new SolidBrush(Color.FromArgb(104, 114, 134));
            using SolidBrush trimBrush = new SolidBrush(Color.FromArgb(176, 62, 66));
            using SolidBrush steelBrush = new SolidBrush(Color.FromArgb(212, 224, 240));
            using Pen shaftPen = new Pen(Color.FromArgb(138, 92, 48), 2.4f);

            g.FillRectangle(armorBrush, cx - 10, cy - 2, 20, 16);
            g.FillRectangle(trimBrush, cx - 10, cy + 4, 20, 4);
            g.FillEllipse(armorBrush, cx - 7, cy - 16, 14, 14);
            g.FillRectangle(Color.FromArgb(30, 36, 48) == Color.Empty ? armorBrush : Brushes.Black, cx - 5, cy - 11, 10, 3);

            g.DrawLine(shaftPen, cx - 14, cy + 15, cx + 16, cy - 16);
            Point[] tip = { new Point(cx + 16, cy - 16), new Point(cx + 22, cy - 21), new Point(cx + 19, cy - 12) };
            g.FillPolygon(steelBrush, tip);
        }

        private static void DrawShamanSprite(Graphics g, int cx, int cy)
        {
            using SolidBrush robeBrush = new SolidBrush(Color.FromArgb(92, 52, 128));
            using SolidBrush maskBrush = new SolidBrush(Color.FromArgb(228, 218, 194));
            using SolidBrush glowBrush = new SolidBrush(Color.FromArgb(94, 224, 104));
            using SolidBrush eyeBrush = new SolidBrush(Color.FromArgb(32, 20, 44));

            Point[] robe = { new Point(cx - 13, cy + 15), new Point(cx - 8, cy - 4), new Point(cx + 8, cy - 4), new Point(cx + 13, cy + 15) };
            g.FillPolygon(robeBrush, robe);

            g.FillEllipse(maskBrush, cx - 7, cy - 16, 14, 14);
            g.FillEllipse(eyeBrush, cx - 4, cy - 12, 3, 3);
            g.FillEllipse(eyeBrush, cx + 1, cy - 12, 3, 3);

            using Pen featherPen = new Pen(Color.FromArgb(94, 212, 98), 2f);
            g.DrawLine(featherPen, cx - 4, cy - 16, cx - 9, cy - 23);
            g.DrawLine(featherPen, cx, cy - 17, cx, cy - 24);
            g.DrawLine(featherPen, cx + 4, cy - 16, cx + 9, cy - 23);

            g.FillEllipse(glowBrush, cx + 9, cy - 4, 9, 10);
            g.FillEllipse(glowBrush, cx - 17, cy + 1, 7, 8);
        }

        private static void DrawVikingChiefSprite(Graphics g, int cx, int cy, float pulsePhase)
        {
            int auraAlpha = 35 + (int)(25 * Math.Sin(pulsePhase * 2.5f));
            using SolidBrush auraBrush = new SolidBrush(Color.FromArgb(Math.Clamp(auraAlpha, 15, 70), 232, 82, 62));
            g.FillEllipse(auraBrush, cx - 28, cy - 26, 56, 48);

            using SolidBrush capeBrush = new SolidBrush(Color.FromArgb(148, 32, 42));
            using SolidBrush armorBrush = new SolidBrush(Color.FromArgb(72, 78, 94));
            using SolidBrush goldBrush = new SolidBrush(Color.FromArgb(242, 198, 86));
            using SolidBrush hornBrush = new SolidBrush(Color.FromArgb(244, 234, 202));
            using SolidBrush steelBrush = new SolidBrush(Color.FromArgb(212, 224, 238));
            using SolidBrush beardBrush = new SolidBrush(Color.FromArgb(142, 72, 28));

            Point[] cape = { new Point(cx - 22, cy + 16), new Point(cx - 14, cy - 8), new Point(cx + 14, cy - 8), new Point(cx + 22, cy + 16) };
            g.FillPolygon(capeBrush, cape);

            g.FillRectangle(armorBrush, cx - 14, cy - 5, 28, 20);
            g.FillRectangle(goldBrush, cx - 14, cy + 3, 28, 4);

            Point[] beard = { new Point(cx - 10, cy - 9), new Point(cx, cy + 4), new Point(cx + 10, cy - 9) };
            g.FillPolygon(beardBrush, beard);

            g.FillPie(armorBrush, cx - 12, cy - 25, 24, 18, 180, 180);
            Point[] crown =
            {
            new Point(cx - 11, cy - 16),
            new Point(cx - 7, cy - 24),
            new Point(cx - 3, cy - 18),
            new Point(cx, cy - 26),
            new Point(cx + 3, cy - 18),
            new Point(cx + 7, cy - 24),
            new Point(cx + 11, cy - 16)
        };
            g.FillPolygon(goldBrush, crown);

            Point[] leftHorn = { new Point(cx - 11, cy - 17), new Point(cx - 23, cy - 27), new Point(cx - 12, cy - 22) };
            Point[] rightHorn = { new Point(cx + 11, cy - 17), new Point(cx + 23, cy - 27), new Point(cx + 12, cy - 22) };
            g.FillPolygon(hornBrush, leftHorn);
            g.FillPolygon(hornBrush, rightHorn);

            using Pen shaft = new Pen(Color.FromArgb(132, 82, 42), 3f);
            g.DrawLine(shaft, cx + 16, cy + 15, cx + 21, cy - 13);
            Point[] bigAxe = { new Point(cx + 19, cy - 13), new Point(cx + 29, cy - 7), new Point(cx + 27, cy + 4), new Point(cx + 18, cy - 1) };
            g.FillPolygon(steelBrush, bigAxe);

            g.DrawLine(shaft, cx - 16, cy + 15, cx - 22, cy - 10);
            using Pen hookPen = new Pen(Color.FromArgb(204, 218, 232), 2.6f);
            g.DrawArc(hookPen, cx - 29, cy - 16, 12, 12, 150, 220);
        }

        private static void DrawImmunityAura(Graphics g, int cx, int cy, float pulsePhase)
        {
            int alpha = 60 + (int)(40 * Math.Sin(pulsePhase * 4f));
            using Pen auraPen = new Pen(Color.FromArgb(Math.Clamp(alpha + 60, 80, 220), 92, 216, 242), 2.5f);
            using SolidBrush fillBrush = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 25, 95), 92, 216, 242));
            g.FillEllipse(fillBrush, cx - 26, cy - 24, 52, 48);
            g.DrawEllipse(auraPen, cx - 26, cy - 24, 52, 48);
        }

        private static void DrawBearTrap(Graphics g, Rectangle tileRect, GroundHazard hazard)
        {
            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2) + 8;

            Color jawColor = hazard.IsArmed ? Color.FromArgb(196, 206, 220) : Color.FromArgb(106, 114, 128);
            Color spikeColor = hazard.IsArmed ? Color.FromArgb(228, 86, 86) : Color.FromArgb(132, 140, 152);

            using Pen jawPen = new Pen(jawColor, 2.4f);
            using SolidBrush plateBrush = new SolidBrush(Color.FromArgb(54, 60, 74));
            using SolidBrush spikeBrush = new SolidBrush(spikeColor);

            g.DrawEllipse(jawPen, cx - 18, cy - 10, 36, 20);
            g.FillEllipse(plateBrush, cx - 7, cy - 4, 14, 8);

            for (int i = -12; i <= 12; i += 8)
            {
                Point[] topSpike = { new Point(cx + i - 3, cy - 9), new Point(cx + i, cy - 15), new Point(cx + i + 3, cy - 9) };
                Point[] botSpike = { new Point(cx + i - 3, cy + 9), new Point(cx + i, cy + 15), new Point(cx + i + 3, cy + 9) };
                g.FillPolygon(spikeBrush, topSpike);
                g.FillPolygon(spikeBrush, botSpike);
            }

            using SolidBrush textBrush = new SolidBrush(hazard.IsArmed ? UiTheme.AccentGold : UiTheme.TextMuted);
            g.DrawString(
                hazard.IsArmed ? "TRAP" : "UNARMED",
                UiTheme.FontBadge,
                textBrush,
                new Rectangle(tileRect.X, tileRect.Bottom - 16, tileRect.Width, 14),
                UiTheme.CenterFormat);
        }

        private static void DrawPoisonPool(Graphics g, Rectangle tileRect, GroundHazard hazard, float pulsePhase)
        {
            int alpha = 85 + (int)(25 * Math.Sin(pulsePhase * 3f));
            using SolidBrush poolBrush = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 55, 130), 64, 198, 78));
            using SolidBrush bubbleBrush = new SolidBrush(Color.FromArgb(165, 146, 248, 132));
            using Pen edgePen = new Pen(Color.FromArgb(140, 94, 224, 102), 1.5f);

            Rectangle mainPool = new Rectangle(tileRect.X + 8, tileRect.Y + 14, tileRect.Width - 16, tileRect.Height - 24);
            g.FillEllipse(poolBrush, mainPool);
            g.DrawEllipse(edgePen, mainPool);

            int bob = (int)(2 * Math.Sin(pulsePhase * 4f));
            g.FillEllipse(bubbleBrush, tileRect.X + 20, tileRect.Y + 26 + bob, 8, 8);
            g.FillEllipse(bubbleBrush, tileRect.Right - 28, tileRect.Y + 34 - bob, 6, 6);
            g.FillEllipse(bubbleBrush, tileRect.X + 34, tileRect.Bottom - 26 + bob, 5, 5);

            using SolidBrush labelBrush = new SolidBrush(Color.FromArgb(196, 255, 196));
            g.DrawString(
                $"POISON {hazard.RemainingTurns}T",
                UiTheme.FontBadge,
                labelBrush,
                new Rectangle(tileRect.X, tileRect.Y + 3, tileRect.Width, 14),
                UiTheme.CenterFormat);
        }

        private static (string Text, Color Color) ResolveEnemyBadge(EnemyInstance enemy, EnemyDefinition def)
        {
            if (enemy.IsStunned)
            {
                return ("STUNNED", UiTheme.AccentGold);
            }

            if (enemy.State == EnemyState.Unarmed)
            {
                return ("EQUIPPING", UiTheme.AccentSlateTop);
            }

            if (enemy.State == EnemyState.Charging)
            {
                string attack = string.IsNullOrWhiteSpace(enemy.LockedAttackType) ? def.WeaponLabel : enemy.LockedAttackType;
                return ($"{attack} ({enemy.ChargeTurnsRemaining}T)", UiTheme.MeleeHeader);
            }

            return (def.WeaponLabel, Color.FromArgb(56, 70, 96));
        }

        private static void DrawTopBadge(Graphics g, Rectangle tileRect, string text, Color bgColor)
        {
            Rectangle badgeRect = new Rectangle(tileRect.X + 3, tileRect.Y + 2, tileRect.Width - 6, 15);
            UiPrimitives.FillRoundedRect(g, badgeRect, 4, Color.FromArgb(215, bgColor));
            using SolidBrush brush = new SolidBrush(UiTheme.TextPrimary);
            g.DrawString(text, UiTheme.FontBadge, brush, badgeRect, UiTheme.CenterFormat);
        }

        private static void DrawNameTag(Graphics g, Rectangle tileRect, string name)
        {
            Rectangle nameRect = new Rectangle(tileRect.X + 2, tileRect.Bottom - 28, tileRect.Width - 4, 13);
            using SolidBrush shadow = new SolidBrush(Color.FromArgb(180, 10, 12, 18));
            using SolidBrush text = new SolidBrush(UiTheme.TextPrimary);
            g.DrawString(name, UiTheme.FontBadge, shadow, new Rectangle(nameRect.X + 1, nameRect.Y + 1, nameRect.Width, nameRect.Height), UiTheme.CenterFormat);
            g.DrawString(name, UiTheme.FontBadge, text, nameRect, UiTheme.CenterFormat);
        }

        private static void DrawHpBar(Graphics g, Rectangle tileRect, int hp, int maxHp, bool isPoisoned, bool isPlayer)
        {
            Rectangle barRect = new Rectangle(tileRect.X + 6, tileRect.Bottom - 14, tileRect.Width - 12, 11);
            UiPrimitives.FillRoundedRect(g, barRect, 3, UiTheme.HpBarBg, UiTheme.BorderSubtle, 1f);

            float ratio = maxHp > 0 ? Math.Clamp((float)hp / maxHp, 0f, 1f) : 0f;
            int fillW = (int)(barRect.Width * ratio);
            if (fillW > 1)
            {
                Color fillColor = isPlayer ? Color.FromArgb(52, 168, 106) : UiTheme.HpBarFill;
                UiPrimitives.FillRoundedRect(g, new Rectangle(barRect.X, barRect.Y, fillW, barRect.Height), 3, fillColor);
            }

            string hpText = isPoisoned ? $"{hp}/{maxHp} PSN" : $"{hp}/{maxHp}";
            using SolidBrush textBrush = new SolidBrush(isPoisoned ? Color.FromArgb(200, 255, 196) : UiTheme.TextPrimary);
            g.DrawString(hpText, UiTheme.FontBadge, textBrush, barRect, UiTheme.CenterFormat);
        }
    }
}