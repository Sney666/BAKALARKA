using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public static class UiPrimitives
    {
        public static void DrawScreenBackground(Graphics g)
        {
            Rectangle bounds = new Rectangle(0, 0, UiTheme.WindowWidth, UiTheme.WindowHeight);
            using LinearGradientBrush brush = new LinearGradientBrush(
            bounds,
            UiTheme.BgTop,
            UiTheme.BgBottom,
            LinearGradientMode.Vertical);
            g.FillRectangle(brush, bounds);
        }
        public static void DrawTopHeaderBar(Graphics g, string title, string subtitle)
        {
            using SolidBrush bg = new SolidBrush(UiTheme.HeaderBg);
            using Pen border = new Pen(UiTheme.BorderSubtle, 1f);
            g.FillRectangle(bg, 0, 0, UiTheme.WindowWidth, 74);
            g.DrawLine(border, 0, 74, UiTheme.WindowWidth, 74);

            using SolidBrush titleBrush = new SolidBrush(UiTheme.AccentGold);
            using SolidBrush subBrush = new SolidBrush(UiTheme.TextSecondary);
            g.DrawString(title, UiTheme.FontTitle, titleBrush, 32f, 10f);
            g.DrawString(subtitle, UiTheme.FontSmall, subBrush, 34f, 47f);
        }

        public static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
            Rectangle arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRoundedRect(
            Graphics g,
            Rectangle rect,
            int radius,
            Color fillColor,
            Color? borderColor = null,
            float borderWidth = 1.5f)
        {
            using GraphicsPath path = CreateRoundedRectPath(rect, radius);
            using SolidBrush fillBrush = new SolidBrush(fillColor);
            g.FillPath(fillBrush, path);

            if (borderColor.HasValue)
            {
                using Pen borderPen = new Pen(borderColor.Value, borderWidth);
                g.DrawPath(borderPen, path);
            }
        }

        public static void DrawGradientPanel(
            Graphics g,
            Rectangle rect,
            int radius,
            Color topColor,
            Color bottomColor,
            Color borderColor,
            float borderWidth = 1.5f)
        {
            using GraphicsPath path = CreateRoundedRectPath(rect, radius);
            using LinearGradientBrush brush = new LinearGradientBrush(rect, topColor, bottomColor, LinearGradientMode.Vertical);
            using Pen pen = new Pen(borderColor, borderWidth);
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        public static void DrawButton(
            Graphics g,
            UiRenderState state,
            Rectangle rect,
            string text,
            string action,
            string payload = "",
            int index = -1,
            bool enabled = true,
            Color? accentTop = null,
            Color? accentBottom = null,
            Font? font = null)
        {
            bool hovered = enabled && state.IsHovered(rect);
            Color top = accentTop ?? UiTheme.AccentBlueTop;
            Color bottom = accentBottom ?? UiTheme.AccentBlueBottom;

            if (!enabled)
            {
                top = Color.FromArgb(42, 46, 58);
                bottom = Color.FromArgb(32, 36, 46);
            }
            else if (hovered)
            {
                top = Brighten(top, 22);
                bottom = Brighten(bottom, 16);
            }

            Color border = !enabled
                ? Color.FromArgb(56, 62, 76)
                : (hovered ? UiTheme.BorderHighlight : Color.FromArgb(92, 118, 160));

            DrawGradientPanel(g, rect, 8, top, bottom, border, hovered ? 2f : 1.5f);

            using SolidBrush textBrush = new SolidBrush(enabled ? UiTheme.TextPrimary : UiTheme.TextMuted);
            g.DrawString(text, font ?? UiTheme.FontBodyBold, textBrush, rect, UiTheme.CenterFormat);

            state.RegisterHit(action, rect, payload, index, enabled);
        }

        public static void DrawStatPill(
            Graphics g,
            Rectangle rect,
            string label,
            string value,
            Color valueColor,
            Color? borderColor = null)
        {
            FillRoundedRect(g, rect, 8, UiTheme.PanelBgAlt, borderColor ?? UiTheme.BorderSubtle, 1.2f);

            Rectangle labelRect = new Rectangle(rect.X + 10, rect.Y + 4, rect.Width - 20, 16);
            Rectangle valueRect = new Rectangle(rect.X + 10, rect.Y + 18, rect.Width - 20, rect.Height - 22);

            using SolidBrush labelBrush = new SolidBrush(UiTheme.TextMuted);
            using SolidBrush valueBrush = new SolidBrush(valueColor);
            g.DrawString(label, UiTheme.FontBadge, labelBrush, labelRect, UiTheme.LeftCenterFormat);
            g.DrawString(value, UiTheme.FontSection, valueBrush, valueRect, UiTheme.LeftCenterFormat);
        }

        public static void DrawCardFace(
            Graphics g,
            Rectangle rect,
            WeaponDefinition weapon,
            int effectiveDamage,
            int effectiveMaxCooldown,
            int currentCooldown,
            IReadOnlyList<string>? modifiers,
            bool hasPoison,
            bool isHovered,
            bool isSelected,
            bool isExecuting,
            bool isDimmed,
            string? footerBadge = null)
        {
            Color headerColor = UiTheme.GetCategoryColor(weapon.Category);
            Color bodyColor = isDimmed ? UiTheme.CardDimBg : UiTheme.CardBg;
            Color borderColor = isExecuting
                ? UiTheme.AccentGold
                : (isSelected ? UiTheme.BorderGold : (isHovered ? UiTheme.BorderHighlight : UiTheme.BorderSubtle));

            FillRoundedRect(g, rect, 9, bodyColor, borderColor, isExecuting || isSelected ? 2.5f : 1.5f);

            Rectangle headerRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 28);
            FillRoundedRect(g, headerRect, 7, isDimmed ? Darken(headerColor, 0.55f) : headerColor);

            using SolidBrush titleBrush = new SolidBrush(isDimmed ? UiTheme.TextSecondary : UiTheme.TextPrimary);
            g.DrawString(
                weapon.Name,
                UiTheme.FontSmallBold,
                titleBrush,
                new Rectangle(headerRect.X + 7, headerRect.Y, headerRect.Width - 14, headerRect.Height),
                UiTheme.LeftCenterFormat);

            DrawCardStatsRow(g, rect, weapon, effectiveDamage, effectiveMaxCooldown, currentCooldown, isDimmed);
            DrawCardDescriptionAndModifiers(g, rect, weapon, modifiers, hasPoison, isDimmed, footerBadge);
        }

        public static void DrawWarningBanner(Graphics g, Rectangle rect, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            FillRoundedRect(g, rect, 6, UiTheme.WarningBg, UiTheme.WarningBorder, 1.5f);
            using SolidBrush brush = new SolidBrush(UiTheme.TextPrimary);
            g.DrawString(message, UiTheme.FontSmallBold, brush, rect, UiTheme.CenterFormat);
        }

        public static void DrawAchievementToast(Graphics g, AchievementToast? toast)
        {
            if (toast == null)
            {
                return;
            }

            Rectangle toastRect = new Rectangle(420, 12, 440, 52);
            DrawGradientPanel(
                g,
                toastRect,
                10,
                Color.FromArgb(44, 58, 34),
                Color.FromArgb(26, 36, 24),
                UiTheme.BorderGold,
                2f);

            using SolidBrush goldBrush = new SolidBrush(UiTheme.AccentGold);
            using SolidBrush whiteBrush = new SolidBrush(UiTheme.TextPrimary);
            g.DrawString(
                $"ACHIEVEMENT UNLOCKED: {toast.Title} (+{toast.CoinReward}c)",
                UiTheme.FontSmallBold,
                goldBrush,
                new Rectangle(toastRect.X + 12, toastRect.Y + 6, toastRect.Width - 24, 18),
                UiTheme.LeftCenterFormat);
            g.DrawString(
                toast.Description,
                UiTheme.FontSmall,
                whiteBrush,
                new Rectangle(toastRect.X + 12, toastRect.Y + 25, toastRect.Width - 24, 20),
                UiTheme.LeftCenterFormat);
        }

        public static Color Brighten(Color color, int delta)
        {
            return Color.FromArgb(
                color.A,
                Math.Min(255, color.R + delta),
                Math.Min(255, color.G + delta),
                Math.Min(255, color.B + delta));
        }

        public static Color Darken(Color color, float factor)
        {
            return Color.FromArgb(
                color.A,
                (int)(color.R * factor),
                (int)(color.G * factor),
                (int)(color.B * factor));
        }

        private static void DrawCardStatsRow(
            Graphics g,
            Rectangle rect,
            WeaponDefinition weapon,
            int effectiveDamage,
            int effectiveMaxCooldown,
            int currentCooldown,
            bool isDimmed)
        {
            int y = rect.Y + 33;
            using SolidBrush catBrush = new SolidBrush(UiTheme.TextMuted);
            using SolidBrush dmgBrush = new SolidBrush(isDimmed ? UiTheme.TextMuted : Color.FromArgb(246, 134, 134));
            using SolidBrush cdBrush = new SolidBrush(currentCooldown > 0 ? UiTheme.AccentGold : UiTheme.ShieldCyan);

            g.DrawString(weapon.Category.ToString().ToUpperInvariant(), UiTheme.FontBadge, catBrush, rect.X + 8, y);

            string statsText = $"DMG {effectiveDamage}  |  CD {(currentCooldown > 0 ? $"{currentCooldown}/{effectiveMaxCooldown}" : effectiveMaxCooldown.ToString())}";
            g.DrawString(
                statsText,
                UiTheme.FontBadge,
                currentCooldown > 0 ? cdBrush : dmgBrush,
                new Rectangle(rect.X + 8, y, rect.Width - 16, 15),
                UiTheme.RightCenterFormat);
        }

        private static void DrawCardDescriptionAndModifiers(
            Graphics g,
            Rectangle rect,
            WeaponDefinition weapon,
            IReadOnlyList<string>? modifiers,
            bool hasPoison,
            bool isDimmed,
            string? footerBadge)
        {
            int descTop = rect.Y + 52;
            int reservedBottom = !string.IsNullOrEmpty(footerBadge) ? 26 : 10;
            bool hasMods = modifiers != null && modifiers.Count > 0;
            if (hasMods)
            {
                reservedBottom += 18;
            }

            Rectangle descRect = new Rectangle(
                rect.X + 8,
                descTop,
                rect.Width - 16,
                Math.Max(20, rect.Bottom - descTop - reservedBottom));

            using SolidBrush descBrush = new SolidBrush(isDimmed ? UiTheme.TextMuted : UiTheme.TextSecondary);
            g.DrawString(weapon.Description, UiTheme.FontSmall, descBrush, descRect, UiTheme.TopLeftWrapFormat);

            if (hasMods && modifiers != null)
            {
                string modLine = string.Join(", ", modifiers);
                Color modColor = hasPoison ? UiTheme.PoisonGreen : UiTheme.AccentGold;
                using SolidBrush modBrush = new SolidBrush(modColor);
                g.DrawString(
                    modLine,
                    UiTheme.FontBadge,
                    modBrush,
                    new Rectangle(rect.X + 8, rect.Bottom - reservedBottom, rect.Width - 16, 16),
                    UiTheme.LeftCenterFormat);
            }

            if (!string.IsNullOrEmpty(footerBadge))
            {
                Rectangle badgeRect = new Rectangle(rect.X + 8, rect.Bottom - 24, rect.Width - 16, 18);
                FillRoundedRect(g, badgeRect, 4, Color.FromArgb(44, 54, 76));
                using SolidBrush badgeBrush = new SolidBrush(UiTheme.AccentGold);
                g.DrawString(footerBadge, UiTheme.FontBadge, badgeBrush, badgeRect, UiTheme.CenterFormat);
            }
        }
    }
}