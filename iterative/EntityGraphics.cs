#nullable disable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RogueCards
{
    public static class EntityGraphics
    {
        public static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int safeRadius = Math.Max(1, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            int diameter = safeRadius * 2;

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        public static GraphicsPath CreateTopRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int safeRadius = Math.Max(1, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            int diameter = safeRadius * 2;

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddLine(bounds.Right, bounds.Bottom, bounds.X, bounds.Bottom);
            path.CloseFigure();

            return path;
        }

        public static void DrawRoundedBox(
            Graphics g,
            Rectangle bounds,
            int radius,
            Color fillColor,
            Color borderColor,
            float borderWidth)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

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

        public static void DrawGradientRoundedBox(
            Graphics g,
            Rectangle bounds,
            int radius,
            Color topColor,
            Color bottomColor,
            Color borderColor,
            float borderWidth)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using (GraphicsPath path = CreateRoundedRectPath(bounds, radius))
            using (LinearGradientBrush fillBrush = new LinearGradientBrush(bounds, topColor, bottomColor, LinearGradientMode.Vertical))
            {
                g.FillPath(fillBrush, path);

                if (borderWidth > 0f && borderColor.A > 0)
                {
                    using (Pen borderPen = new Pen(borderColor, borderWidth))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }
            }
        }

        public static void DrawCoinIcon(Graphics g, int cx, int cy, int radius)
        {
            Rectangle outer = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
            if (outer.Width <= 0 || outer.Height <= 0)
            {
                return;
            }

            using (LinearGradientBrush coinBrush = new LinearGradientBrush(
                outer,
                Color.FromArgb(255, 224, 112),
                Color.FromArgb(196, 134, 24),
                LinearGradientMode.ForwardDiagonal))
            using (Pen rimPen = new Pen(Color.FromArgb(255, 242, 174), 1.4f))
            using (Pen innerPen = new Pen(Color.FromArgb(158, 102, 18), 1.2f))
            {
                g.FillEllipse(coinBrush, outer);
                g.DrawEllipse(rimPen, outer);

                int innerR = Math.Max(2, radius - 3);
                g.DrawEllipse(innerPen, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
            }
        }

        public static void DrawPoisonVialIcon(Graphics g, int cx, int cy, int size)
        {
            float s = size / 16f;
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(s, s);

            using (SolidBrush corkBrush = new SolidBrush(Color.FromArgb(164, 118, 72)))
            using (SolidBrush glassBrush = new SolidBrush(Color.FromArgb(180, 210, 235, 220)))
            using (LinearGradientBrush liquidBrush = new LinearGradientBrush(
                new Rectangle(-6, -2, 12, 10),
                Color.FromArgb(118, 246, 146),
                Color.FromArgb(34, 148, 72),
                LinearGradientMode.Vertical))
            using (Pen outlinePen = new Pen(Color.FromArgb(215, 250, 225), 1.2f))
            {
                g.FillRectangle(corkBrush, -2.5f, -8f, 5f, 3f);
                g.FillRectangle(glassBrush, -2f, -5.5f, 4f, 3.5f);
                g.FillEllipse(liquidBrush, -5.5f, -2.5f, 11f, 10.5f);
                g.DrawEllipse(outlinePen, -5.5f, -2.5f, 11f, 10.5f);

                using (SolidBrush gleam = new SolidBrush(Color.FromArgb(185, 255, 255, 255)))
                {
                    g.FillEllipse(gleam, -3f, -0.5f, 2.5f, 4f);
                }
            }

            g.Restore(state);
        }

        public static void DrawCategoryIcon(Graphics g, CardCategory category, int cx, int cy, int size)
        {
            float s = size / 16f;
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(s, s);

            using (Pen whitePen = new Pen(Color.White, 1.8f))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            {
                whitePen.StartCap = LineCap.Round;
                whitePen.EndCap = LineCap.Round;

                if (category == CardCategory.Melee)
                {
                    g.DrawLine(whitePen, -5, 5, 5, -5);
                    g.DrawLine(whitePen, -3, 1, 1, 5);
                    PointF[] tip = new PointF[]
                    {
                        new PointF(2, -6),
                        new PointF(6.5f, -6.5f),
                        new PointF(6, -2)
                    };
                    g.FillPolygon(whiteBrush, tip);
                }
                else if (category == CardCategory.Ranged)
                {
                    g.DrawArc(whitePen, -6, -6, 10, 12, -80, 160);
                    g.DrawLine(whitePen, -4, 0, 6, 0);
                    PointF[] head = new PointF[]
                    {
                        new PointF(3, -3),
                        new PointF(7, 0),
                        new PointF(3, 3)
                    };
                    g.FillPolygon(whiteBrush, head);
                }
                else if (category == CardCategory.Movement)
                {
                    g.DrawLine(whitePen, -6, 3, 1, -4);
                    g.DrawLine(whitePen, 1, -4, 6, 1);
                    g.DrawLine(whitePen, -6, 6, 1, 0);
                    g.DrawLine(whitePen, 1, 0, 6, 5);
                }
                else
                {
                    PointF[] diamond = new PointF[]
                    {
                        new PointF(0, -6.5f),
                        new PointF(2.2f, -2.2f),
                        new PointF(6.5f, 0),
                        new PointF(2.2f, 2.2f),
                        new PointF(0, 6.5f),
                        new PointF(-2.2f, 2.2f),
                        new PointF(-6.5f, 0),
                        new PointF(-2.2f, -2.2f)
                    };
                    g.FillPolygon(whiteBrush, diamond);
                }
            }

            g.Restore(state);
        }

        public static void DrawTrophyIcon(Graphics g, int cx, int cy, int radius, bool unlocked)
        {
            float s = radius / 12f;
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(s, s);

            Color topMetal = unlocked ? Color.FromArgb(255, 224, 110) : Color.FromArgb(118, 128, 144);
            Color botMetal = unlocked ? Color.FromArgb(186, 126, 24) : Color.FromArgb(72, 80, 96);
            Color rimColor = unlocked ? Color.FromArgb(255, 244, 184) : Color.FromArgb(136, 148, 166);

            using (LinearGradientBrush cupBrush = new LinearGradientBrush(
                new Rectangle(-8, -9, 16, 18),
                topMetal,
                botMetal,
                LinearGradientMode.Vertical))
            using (Pen rimPen = new Pen(rimColor, 1.3f))
            {
                g.DrawArc(rimPen, -10, -7, 5, 7, 90, 180);
                g.DrawArc(rimPen, 5, -7, 5, 7, -90, 180);

                PointF[] cup = new PointF[]
                {
                    new PointF(-6.5f, -8f),
                    new PointF(6.5f, -8f),
                    new PointF(4.8f, 1.5f),
                    new PointF(0f, 4f),
                    new PointF(-4.8f, 1.5f)
                };
                g.FillPolygon(cupBrush, cup);
                g.DrawPolygon(rimPen, cup);

                g.FillRectangle(cupBrush, -1.6f, 3.5f, 3.2f, 4.5f);
                g.FillRectangle(cupBrush, -5.5f, 7.5f, 11f, 2.8f);
                g.DrawRectangle(rimPen, -5.5f, 7.5f, 11f, 2.8f);
            }

            g.Restore(state);
        }

        public static void DrawLockIcon(Graphics g, int cx, int cy)
        {
            Rectangle body = new Rectangle(cx - 11, cy - 3, 22, 18);
            using (Pen shacklePen = new Pen(Color.FromArgb(236, 206, 128), 2.8f))
            using (LinearGradientBrush bodyBrush = new LinearGradientBrush(
                body,
                Color.FromArgb(242, 192, 74),
                Color.FromArgb(172, 118, 26),
                LinearGradientMode.Vertical))
            using (Pen rimPen = new Pen(Color.FromArgb(255, 232, 154), 1.2f))
            using (SolidBrush holeBrush = new SolidBrush(Color.FromArgb(26, 30, 38)))
            {
                g.DrawArc(shacklePen, cx - 7, cy - 14, 14, 18, 180, 180);
                using (GraphicsPath bodyPath = CreateRoundedRectPath(body, 4))
                {
                    g.FillPath(bodyBrush, bodyPath);
                    g.DrawPath(rimPen, bodyPath);
                }

                g.FillEllipse(holeBrush, cx - 2.5f, cy + 2f, 5f, 5f);
                g.FillRectangle(holeBrush, cx - 1.3f, cy + 5f, 2.6f, 5.5f);
            }
        }

        public static void DrawBearTrapHazard(Graphics g, Rectangle tileRect, bool isArmed)
        {
            int cx = tileRect.X + (tileRect.Width / 2);
            int cy = tileRect.Y + (tileRect.Height / 2) - 2;

            Color jawTop = isArmed ? Color.FromArgb(228, 184, 78) : Color.FromArgb(132, 142, 158);
            Color jawBot = isArmed ? Color.FromArgb(148, 92, 28) : Color.FromArgb(84, 92, 108);
            Color plateColor = isArmed ? Color.FromArgb(204, 64, 52) : Color.FromArgb(98, 106, 122);

            if (isArmed)
            {
                using (SolidBrush aura = new SolidBrush(Color.FromArgb(46, 232, 164, 54)))
                {
                    g.FillEllipse(aura, cx - 25, cy - 25, 50, 50);
                }
            }

            Rectangle ringRect = new Rectangle(cx - 19, cy - 19, 38, 38);
            using (LinearGradientBrush metalBrush = new LinearGradientBrush(ringRect, jawTop, jawBot, LinearGradientMode.Vertical))
            using (Pen jawPen = new Pen(metalBrush, 3.2f))
            using (Pen toothPen = new Pen(isArmed ? Color.FromArgb(245, 224, 152) : Color.FromArgb(164, 174, 190), 1.8f))
            using (SolidBrush plateBrush = new SolidBrush(plateColor))
            {
                g.DrawEllipse(jawPen, ringRect);

                for (int i = 0; i < 10; i++)
                {
                    double angle = (Math.PI * 2.0 / 10.0) * i;
                    int x1 = cx + (int)(Math.Cos(angle) * 14);
                    int y1 = cy + (int)(Math.Sin(angle) * 14);
                    int x2 = cx + (int)(Math.Cos(angle) * 23);
                    int y2 = cy + (int)(Math.Sin(angle) * 23);
                    g.DrawLine(toothPen, x1, y1, x2, y2);
                }

                g.FillEllipse(plateBrush, cx - 9, cy - 9, 18, 18);
                g.DrawEllipse(toothPen, cx - 9, cy - 9, 18, 18);
            }

            using (Font trapFont = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (SolidBrush labelBg = new SolidBrush(Color.FromArgb(185, 16, 20, 28)))
            using (SolidBrush labelBrush = new SolidBrush(isArmed ? Color.FromArgb(246, 206, 98) : Color.FromArgb(156, 169, 189)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                Rectangle labelRect = new Rectangle(tileRect.X + 4, tileRect.Bottom - 16, tileRect.Width - 8, 13);
                g.FillRectangle(labelBg, labelRect);
                g.DrawString(isArmed ? "BEAR TRAP" : "TRAP SET", trapFont, labelBrush, labelRect, sf);
            }
        }

        public static void DrawPoisonPoolHazard(Graphics g, Rectangle tileRect, int remainingTurns, float pulsePhase)
        {
            double wave = 0.5 + (0.5 * Math.Sin(pulsePhase * 1.3));
            int alpha = 82 + (int)(wave * 44);
            Rectangle inner = new Rectangle(tileRect.X + 4, tileRect.Y + 4, tileRect.Width - 8, tileRect.Height - 8);

            using (GraphicsPath puddle = CreateRoundedRectPath(inner, 10))
            using (LinearGradientBrush toxicBrush = new LinearGradientBrush(
                inner,
                Color.FromArgb(alpha, 82, 232, 118),
                Color.FromArgb(alpha + 20, 28, 132, 62),
                LinearGradientMode.ForwardDiagonal))
            using (Pen glowPen = new Pen(Color.FromArgb(185, 138, 250, 162), 1.5f))
            {
                g.FillPath(toxicBrush, puddle);
                g.DrawPath(glowPen, puddle);

                int cx = tileRect.X + (tileRect.Width / 2);
                int cy = tileRect.Y + (tileRect.Height / 2);
                int bOffset = (int)(wave * 3);

                g.DrawEllipse(glowPen, cx - 18, cy - 10 - bOffset, 12, 12);
                g.DrawEllipse(glowPen, cx + 6, cy - 16 + bOffset, 9, 9);
                g.DrawEllipse(glowPen, cx - 3, cy + 4 - bOffset, 14, 14);
            }

            if (remainingTurns > 0)
            {
                Rectangle badge = new Rectangle(tileRect.X + 5, tileRect.Bottom - 16, 40, 12);
                DrawRoundedBox(g, badge, 3, Color.FromArgb(200, 18, 48, 28), Color.FromArgb(132, 242, 158), 1f);
                using (Font pFont = new Font("Segoe UI", 6.2f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(172, 255, 192)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString($"TOX {remainingTurns}T", pFont, textBrush, badge, sf);
                }
            }
        }

        public static void DrawViking(Graphics g, int cx, int cy, float scale)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(95, 0, 0, 0)))
            {
                g.FillEllipse(shadow, -19, 17, 38, 9);
            }

            Rectangle cloakBounds = new Rectangle(-18, 2, 36, 20);
            using (LinearGradientBrush cloakBrush = new LinearGradientBrush(
                cloakBounds,
                Color.FromArgb(58, 122, 212),
                Color.FromArgb(28, 64, 124),
                LinearGradientMode.Vertical))
            using (Pen goldPen = new Pen(Color.FromArgb(234, 184, 68), 1.6f))
            {
                Point[] cloak = new Point[]
                {
                    new Point(-17, 21),
                    new Point(-14, 4),
                    new Point(14, 4),
                    new Point(17, 21)
                };
                g.FillPolygon(cloakBrush, cloak);
                g.DrawPolygon(goldPen, cloak);

                g.FillEllipse(new SolidBrush(Color.FromArgb(228, 174, 62)), -4, 5, 8, 5);
            }

            using (SolidBrush skinBrush = new SolidBrush(Color.FromArgb(232, 190, 154)))
            using (LinearGradientBrush beardBrush = new LinearGradientBrush(
                new Rectangle(-10, -2, 20, 18),
                Color.FromArgb(208, 134, 54),
                Color.FromArgb(136, 74, 24),
                LinearGradientMode.Vertical))
            using (SolidBrush eyeBrush = new SolidBrush(Color.FromArgb(24, 30, 42)))
            {
                g.FillEllipse(skinBrush, -9, -11, 18, 18);

                Point[] beard = new Point[]
                {
                    new Point(-9, -1),
                    new Point(9, -1),
                    new Point(7, 10),
                    new Point(0, 16),
                    new Point(-7, 10)
                };
                g.FillPolygon(beardBrush, beard);

                g.FillRectangle(eyeBrush, -5, -5, 3, 3);
                g.FillRectangle(eyeBrush, 2, -5, 3, 3);
            }

            using (LinearGradientBrush hornBrush = new LinearGradientBrush(
                new Rectangle(-24, -24, 48, 20),
                Color.FromArgb(248, 240, 216),
                Color.FromArgb(196, 180, 146),
                LinearGradientMode.Vertical))
            using (Pen hornPen = new Pen(Color.FromArgb(128, 112, 86), 1f))
            {
                Point[] leftHorn = new Point[]
                {
                    new Point(-9, -9),
                    new Point(-22, -14),
                    new Point(-17, -24),
                    new Point(-13, -15),
                    new Point(-7, -13)
                };
                Point[] rightHorn = new Point[]
                {
                    new Point(9, -9),
                    new Point(22, -14),
                    new Point(17, -24),
                    new Point(13, -15),
                    new Point(7, -13)
                };
                g.FillPolygon(hornBrush, leftHorn);
                g.DrawPolygon(hornPen, leftHorn);
                g.FillPolygon(hornBrush, rightHorn);
                g.DrawPolygon(hornPen, rightHorn);
            }

            Rectangle helmBounds = new Rectangle(-12, -20, 24, 20);
            using (LinearGradientBrush steelBrush = new LinearGradientBrush(
                helmBounds,
                Color.FromArgb(192, 208, 228),
                Color.FromArgb(104, 118, 142),
                LinearGradientMode.Vertical))
            using (Pen goldPen = new Pen(Color.FromArgb(234, 184, 68), 1.8f))
            {
                g.FillPie(steelBrush, -11, -19, 22, 19, 180, 180);
                g.DrawArc(goldPen, -11, -19, 22, 19, 180, 180);
                g.DrawLine(goldPen, -11, -9, 11, -9);
                g.DrawLine(goldPen, 0, -19, 0, -5);
            }

            using (Pen shaftPen = new Pen(Color.FromArgb(148, 96, 52), 2.5f))
            using (LinearGradientBrush bladeBrush = new LinearGradientBrush(
                new Rectangle(16, -15, 13, 18),
                Color.FromArgb(232, 240, 250),
                Color.FromArgb(132, 148, 170),
                LinearGradientMode.Horizontal))
            using (Pen edgePen = new Pen(Color.FromArgb(245, 250, 255), 1.1f))
            {
                g.DrawLine(shaftPen, 15, 20, 21, -11);
                Point[] blade = new Point[]
                {
                    new Point(19, -9),
                    new Point(28, -14),
                    new Point(27, 1),
                    new Point(18, -2)
                };
                g.FillPolygon(bladeBrush, blade);
                g.DrawPolygon(edgePen, blade);
            }

            g.Restore(state);
        }

        public static void DrawShieldmaiden(Graphics g, int cx, int cy, float scale, bool dimmedLocked)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);

            Color armorTop = dimmedLocked ? Color.FromArgb(88, 98, 112) : Color.FromArgb(72, 188, 216);
            Color armorBot = dimmedLocked ? Color.FromArgb(52, 58, 70) : Color.FromArgb(34, 108, 138);
            Color metalTop = dimmedLocked ? Color.FromArgb(142, 150, 164) : Color.FromArgb(232, 242, 252);
            Color metalBot = dimmedLocked ? Color.FromArgb(92, 98, 112) : Color.FromArgb(142, 162, 184);
            Color runeColor = dimmedLocked ? Color.FromArgb(136, 144, 158) : Color.FromArgb(108, 232, 255);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(95, 0, 0, 0)))
            {
                g.FillEllipse(shadow, -18, 17, 36, 9);
            }

            Rectangle bodyRect = new Rectangle(-17, 2, 34, 20);
            using (LinearGradientBrush armorBrush = new LinearGradientBrush(bodyRect, armorTop, armorBot, LinearGradientMode.Vertical))
            using (Pen silverPen = new Pen(metalTop, 1.5f))
            {
                Point[] body = new Point[]
                {
                    new Point(-16, 21),
                    new Point(-12, 4),
                    new Point(12, 4),
                    new Point(16, 21)
                };
                g.FillPolygon(armorBrush, body);
                g.DrawPolygon(silverPen, body);
            }

            using (SolidBrush skinBrush = new SolidBrush(dimmedLocked ? Color.FromArgb(148, 138, 132) : Color.FromArgb(236, 204, 178)))
            using (SolidBrush eyeBrush = new SolidBrush(Color.FromArgb(24, 30, 42)))
            {
                g.FillEllipse(skinBrush, -8, -11, 16, 17);
                g.FillRectangle(eyeBrush, -4, -5, 2.5f, 3f);
                g.FillRectangle(eyeBrush, 2, -5, 2.5f, 3f);
            }

            using (LinearGradientBrush wingBrush = new LinearGradientBrush(
                new Rectangle(-24, -22, 48, 18),
                metalTop,
                metalBot,
                LinearGradientMode.Vertical))
            {
                Point[] leftWing = new Point[]
                {
                    new Point(-9, -8),
                    new Point(-23, -21),
                    new Point(-17, -12),
                    new Point(-20, -7),
                    new Point(-9, -5)
                };
                Point[] rightWing = new Point[]
                {
                    new Point(9, -8),
                    new Point(23, -21),
                    new Point(17, -12),
                    new Point(20, -7),
                    new Point(9, -5)
                };
                g.FillPolygon(wingBrush, leftWing);
                g.FillPolygon(wingBrush, rightWing);
            }

            using (LinearGradientBrush helmBrush = new LinearGradientBrush(
                new Rectangle(-11, -19, 22, 18),
                metalTop,
                metalBot,
                LinearGradientMode.Vertical))
            using (Pen runePen = new Pen(runeColor, 1.6f))
            {
                g.FillPie(helmBrush, -10, -19, 20, 18, 180, 180);
                g.DrawLine(runePen, -10, -10, 10, -10);
                g.DrawLine(runePen, 0, -19, 0, -6);
            }

            Rectangle shieldRect = new Rectangle(3, 0, 23, 23);
            using (LinearGradientBrush shieldFill = new LinearGradientBrush(
                shieldRect,
                dimmedLocked ? Color.FromArgb(52, 60, 72) : Color.FromArgb(36, 86, 112),
                dimmedLocked ? Color.FromArgb(30, 36, 46) : Color.FromArgb(18, 42, 58),
                LinearGradientMode.ForwardDiagonal))
            using (Pen rimPen = new Pen(metalTop, 2.1f))
            using (Pen runePen = new Pen(runeColor, 1.5f))
            using (SolidBrush bossBrush = new SolidBrush(metalTop))
            {
                g.FillEllipse(shieldFill, shieldRect);
                g.DrawEllipse(rimPen, shieldRect);
                g.DrawLine(runePen, shieldRect.X + 11.5f, shieldRect.Y + 4, shieldRect.X + 11.5f, shieldRect.Bottom - 4);
                g.DrawLine(runePen, shieldRect.X + 4, shieldRect.Y + 11.5f, shieldRect.Right - 4, shieldRect.Y + 11.5f);
                g.FillEllipse(bossBrush, shieldRect.X + 8.5f, shieldRect.Y + 8.5f, 6, 6);
            }

            g.Restore(state);
        }

        public static void DrawPawn(Graphics g, int cx, int cy, float scale)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillEllipse(shadow, -16, 13, 32, 8);
            }

            using (LinearGradientBrush tunicBrush = new LinearGradientBrush(
                new Rectangle(-11, -3, 22, 18),
                Color.FromArgb(178, 72, 66),
                Color.FromArgb(106, 38, 38),
                LinearGradientMode.Vertical))
            using (SolidBrush skinBrush = new SolidBrush(Color.FromArgb(224, 184, 150)))
            using (LinearGradientBrush helmBrush = new LinearGradientBrush(
                new Rectangle(-9, -18, 18, 14),
                Color.FromArgb(184, 196, 214),
                Color.FromArgb(102, 112, 128),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(tunicBrush, -10, -2, 20, 16);
                g.FillEllipse(skinBrush, -7, -13, 14, 13);
                g.FillPie(helmBrush, -8, -17, 16, 13, 180, 180);
            }

            Rectangle shieldRect = new Rectangle(-22, -5, 16, 16);
            using (LinearGradientBrush woodBrush = new LinearGradientBrush(
                shieldRect,
                Color.FromArgb(164, 112, 62),
                Color.FromArgb(98, 62, 32),
                LinearGradientMode.ForwardDiagonal))
            using (Pen rimPen = new Pen(Color.FromArgb(196, 206, 220), 1.8f))
            using (SolidBrush bossBrush = new SolidBrush(Color.FromArgb(214, 224, 238)))
            {
                g.FillEllipse(woodBrush, shieldRect);
                g.DrawEllipse(rimPen, shieldRect);
                g.FillEllipse(bossBrush, shieldRect.X + 6, shieldRect.Y + 6, 4, 4);
            }

            using (Pen shaftPen = new Pen(Color.FromArgb(142, 94, 52), 2.3f))
            using (LinearGradientBrush bladeBrush = new LinearGradientBrush(
                new Rectangle(14, -13, 12, 12),
                Color.FromArgb(228, 236, 248),
                Color.FromArgb(132, 144, 162),
                LinearGradientMode.Horizontal))
            {
                g.DrawLine(shaftPen, 12, 13, 18, -10);
                Point[] blade = new Point[]
                {
                    new Point(16, -9),
                    new Point(25, -13),
                    new Point(24, -2),
                    new Point(15, -3)
                };
                g.FillPolygon(bladeBrush, blade);
            }

            g.Restore(state);
        }

        public static void DrawArcher(Graphics g, int cx, int cy, float scale)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillEllipse(shadow, -15, 13, 30, 8);
            }

            using (LinearGradientBrush cloakBrush = new LinearGradientBrush(
                new Rectangle(-13, -5, 26, 20),
                Color.FromArgb(62, 146, 92),
                Color.FromArgb(28, 76, 46),
                LinearGradientMode.Vertical))
            using (SolidBrush hoodBrush = new SolidBrush(Color.FromArgb(34, 96, 58)))
            using (SolidBrush skinBrush = new SolidBrush(Color.FromArgb(224, 188, 156)))
            {
                Point[] cloak = new Point[]
                {
                    new Point(-12, 15),
                    new Point(-9, -3),
                    new Point(9, -3),
                    new Point(12, 15)
                };
                g.FillPolygon(cloakBrush, cloak);
                g.FillEllipse(skinBrush, -6, -13, 12, 12);
                Point[] hood = new Point[]
                {
                    new Point(-10, -5),
                    new Point(0, -20),
                    new Point(10, -5)
                };
                g.FillPolygon(hoodBrush, hood);
            }

            using (Pen bowPen = new Pen(Color.FromArgb(196, 138, 68), 2.5f))
            using (Pen stringPen = new Pen(Color.FromArgb(232, 238, 246), 1.1f))
            using (Pen arrowPen = new Pen(Color.FromArgb(218, 228, 240), 1.6f))
            {
                g.DrawArc(bowPen, 4, -14, 16, 28, -80, 160);
                g.DrawLine(stringPen, 12, -13, 12, 13);
                g.DrawLine(arrowPen, 5, 0, 23, 0);
            }

            g.Restore(state);
        }

        public static void DrawSpearman(Graphics g, int cx, int cy, float scale)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillEllipse(shadow, -16, 13, 32, 8);
            }

            using (LinearGradientBrush armorBrush = new LinearGradientBrush(
                new Rectangle(-12, -4, 24, 19),
                Color.FromArgb(136, 154, 182),
                Color.FromArgb(72, 84, 108),
                LinearGradientMode.Vertical))
            using (Pen trimPen = new Pen(Color.FromArgb(204, 218, 238), 1.4f))
            using (SolidBrush skinBrush = new SolidBrush(Color.FromArgb(220, 182, 150)))
            {
                g.FillRectangle(armorBrush, -11, -3, 22, 17);
                g.DrawRectangle(trimPen, -11, -3, 22, 17);
                g.FillEllipse(skinBrush, -7, -13, 14, 13);
                g.FillPie(armorBrush, -9, -18, 18, 14, 180, 180);
            }

            using (Pen spearPen = new Pen(Color.FromArgb(164, 114, 62), 2.4f))
            using (SolidBrush tipBrush = new SolidBrush(Color.FromArgb(232, 242, 252)))
            using (SolidBrush tasselBrush = new SolidBrush(Color.FromArgb(208, 64, 64)))
            {
                g.DrawLine(spearPen, -17, 15, 18, -15);
                g.FillPolygon(tasselBrush, new Point[] { new Point(12, -10), new Point(16, -6), new Point(9, -4) });
                Point[] tip = new Point[]
                {
                    new Point(15, -17),
                    new Point(26, -21),
                    new Point(21, -11)
                };
                g.FillPolygon(tipBrush, tip);
            }

            g.Restore(state);
        }

        public static void DrawShaman(Graphics g, int cx, int cy, float scale)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillEllipse(shadow, -16, 13, 32, 8);
            }

            using (LinearGradientBrush robeBrush = new LinearGradientBrush(
                new Rectangle(-14, -5, 28, 21),
                Color.FromArgb(128, 72, 174),
                Color.FromArgb(62, 34, 92),
                LinearGradientMode.Vertical))
            using (SolidBrush maskBrush = new SolidBrush(Color.FromArgb(226, 212, 174)))
            using (SolidBrush eyeBrush = new SolidBrush(Color.FromArgb(96, 246, 136)))
            using (Pen featherPen = new Pen(Color.FromArgb(212, 156, 62), 1.8f))
            {
                Point[] robe = new Point[]
                {
                    new Point(-13, 15),
                    new Point(-8, -4),
                    new Point(8, -4),
                    new Point(13, 15)
                };
                g.FillPolygon(robeBrush, robe);
                g.DrawLine(featherPen, -6, -15, -12, -22);
                g.DrawLine(featherPen, 0, -16, 0, -23);
                g.DrawLine(featherPen, 6, -15, 12, -22);

                g.FillEllipse(maskBrush, -8, -16, 16, 16);
                g.FillEllipse(eyeBrush, -5, -10, 4, 4);
                g.FillEllipse(eyeBrush, 1, -10, 4, 4);
            }

            DrawPoisonVialIcon(g, 16, 4, 14);
            DrawPoisonVialIcon(g, -16, 6, 12);

            g.Restore(state);
        }

        public static void DrawVikingChief(Graphics g, int cx, int cy, float scale)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(115, 0, 0, 0)))
            {
                g.FillEllipse(shadow, -23, 14, 46, 10);
            }

            using (LinearGradientBrush capeBrush = new LinearGradientBrush(
                new Rectangle(-22, -5, 44, 23),
                Color.FromArgb(182, 42, 46),
                Color.FromArgb(96, 20, 24),
                LinearGradientMode.Vertical))
            using (Pen goldPen = new Pen(Color.FromArgb(242, 192, 68), 2.1f))
            {
                Point[] body = new Point[]
                {
                    new Point(-21, 17),
                    new Point(-16, -4),
                    new Point(16, -4),
                    new Point(21, 17)
                };
                g.FillPolygon(capeBrush, body);
                g.DrawPolygon(goldPen, body);
                g.DrawLine(goldPen, -14, 2, 14, 2);
            }

            using (SolidBrush skinBrush = new SolidBrush(Color.FromArgb(228, 182, 146)))
            using (SolidBrush beardBrush = new SolidBrush(Color.FromArgb(118, 66, 30)))
            {
                g.FillEllipse(skinBrush, -10, -14, 20, 18);
                Point[] beard = new Point[]
                {
                    new Point(-10, -4),
                    new Point(10, -4),
                    new Point(8, 8),
                    new Point(0, 13),
                    new Point(-8, 8)
                };
                g.FillPolygon(beardBrush, beard);
            }

            using (SolidBrush hornBrush = new SolidBrush(Color.FromArgb(248, 238, 206)))
            using (LinearGradientBrush crownBrush = new LinearGradientBrush(
                new Rectangle(-12, -24, 24, 15),
                Color.FromArgb(255, 226, 114),
                Color.FromArgb(192, 132, 26),
                LinearGradientMode.Vertical))
            {
                Point[] leftHorn = new Point[]
                {
                    new Point(-10, -12),
                    new Point(-25, -18),
                    new Point(-20, -27),
                    new Point(-14, -18)
                };
                Point[] rightHorn = new Point[]
                {
                    new Point(10, -12),
                    new Point(25, -18),
                    new Point(20, -27),
                    new Point(14, -18)
                };
                g.FillPolygon(hornBrush, leftHorn);
                g.FillPolygon(hornBrush, rightHorn);

                Point[] crown = new Point[]
                {
                    new Point(-11, -11),
                    new Point(-10, -21),
                    new Point(-4, -15),
                    new Point(0, -24),
                    new Point(4, -15),
                    new Point(10, -21),
                    new Point(11, -11)
                };
                g.FillPolygon(crownBrush, crown);
            }

            using (Pen shaftPen = new Pen(Color.FromArgb(156, 102, 54), 2.6f))
            using (SolidBrush steelBrush = new SolidBrush(Color.FromArgb(224, 234, 246)))
            {
                g.DrawLine(shaftPen, -21, 13, -26, -10);
                g.FillPolygon(steelBrush, new Point[] { new Point(-26, -11), new Point(-32, -6), new Point(-25, 0) });

                g.DrawLine(shaftPen, 21, 13, 26, -10);
                g.FillPolygon(steelBrush, new Point[] { new Point(26, -11), new Point(32, -6), new Point(25, 0) });
            }

            g.Restore(state);
        }
    }
}