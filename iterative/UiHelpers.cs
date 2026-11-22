#nullable disable
using System.Drawing.Drawing2D;

namespace RogueCards
{
    public static class UiTheme
    {
        public const int WindowWidth = 1280;
        public const int WindowHeight = 720;

        public static readonly Color Background = Color.FromArgb(18, 22, 29);
        public static readonly Color BgTop = Color.FromArgb(15, 19, 26);
        public static readonly Color BgBottom = Color.FromArgb(23, 29, 40);
        public static readonly Color MenuBgBottom = Color.FromArgb(24, 30, 41);
        public static readonly Color Header = Color.FromArgb(20, 25, 34);
        public static readonly Color Panel = Color.FromArgb(27, 33, 44);
        public static readonly Color CardBg = Color.FromArgb(34, 42, 56);
        public static readonly Color Border = Color.FromArgb(58, 70, 90);
        public static readonly Color Gold = Color.FromArgb(228, 174, 62);
        public static readonly Color GoldDark = Color.FromArgb(166, 118, 28);
        public static readonly Color Cyan = Color.FromArgb(72, 189, 214);
        public static readonly Color Red = Color.FromArgb(212, 74, 74);
        public static readonly Color Orange = Color.FromArgb(232, 128, 48);
        public static readonly Color Green = Color.FromArgb(62, 166, 107);
        public static readonly Color Purple = Color.FromArgb(148, 86, 196);
        public static readonly Color TextPrimary = Color.FromArgb(238, 242, 248);
        public static readonly Color TextSecondary = Color.FromArgb(157, 170, 192);
        public static readonly Color Muted = Color.FromArgb(108, 120, 140);
        public static readonly Color DarkButtonText = Color.FromArgb(18, 20, 26);
    }

    public static class UiHelpers
    {
        public static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new();
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
            GraphicsPath path = new();
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

            using GraphicsPath path = CreateRoundedRectPath(bounds, radius);

            if (fillColor.A > 0)
            {
                using SolidBrush fillBrush = new(fillColor);
                g.FillPath(fillBrush, path);
            }

            if (borderWidth > 0f && borderColor.A > 0)
            {
                using Pen borderPen = new(borderColor, borderWidth);
                g.DrawPath(borderPen, path);
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

            using GraphicsPath path = CreateRoundedRectPath(bounds, radius);
            using LinearGradientBrush fillBrush = new(bounds, topColor, bottomColor, LinearGradientMode.Vertical);
            g.FillPath(fillBrush, path);

            if (borderWidth > 0f && borderColor.A > 0)
            {
                using Pen borderPen = new(borderColor, borderWidth);
                g.DrawPath(borderPen, path);
            }
        }

        public static void DrawBackground(Graphics g, Color bottomColor)
        {
            Rectangle fullRect = new(0, 0, UiTheme.WindowWidth, UiTheme.WindowHeight);
            using LinearGradientBrush brush = new(fullRect, UiTheme.BgTop, bottomColor, LinearGradientMode.Vertical);
            g.FillRectangle(brush, fullRect);
        }

        public static void DrawHeaderBar(Graphics g, int height)
        {
            Rectangle barRect = new(0, 0, UiTheme.WindowWidth, height);
            using SolidBrush barBrush = new(UiTheme.Header);
            g.FillRectangle(barBrush, barRect);

            using Pen sepPen = new(UiTheme.Border, 1.5f);
            g.DrawLine(sepPen, 0, height, UiTheme.WindowWidth, height);
        }

        public static void DrawCardHeader(
            Graphics g,
            Rectangle cardRect,
            int height,
            int radius,
            Color topColor,
            Color bottomColor)
        {
            Rectangle headerRect = new(cardRect.X + 2, cardRect.Y + 2, cardRect.Width - 4, height);
            using GraphicsPath headerPath = CreateTopRoundedRectPath(headerRect, radius);
            using LinearGradientBrush headerBrush = new(headerRect, topColor, bottomColor, LinearGradientMode.Vertical);
            g.FillPath(headerBrush, headerPath);
        }

        public static void DrawStatPill(Graphics g, Rectangle rect, string text, Color textColor, float fontSize = 8.5f)
        {
            DrawRoundedBox(g, rect, 5, Color.FromArgb(24, 30, 41), Color.FromArgb(52, 64, 82), 1f);
            using Font pillFont = new("Segoe UI", fontSize, FontStyle.Bold);
            using SolidBrush brush = new(textColor);
            using StringFormat sf = CreateStringFormat();
            g.DrawString(text, pillFont, brush, rect, sf);
        }

        public static void DrawGradientButton(
            Graphics g,
            Rectangle bounds,
            Color topColor,
            Color bottomColor,
            Color borderColor,
            string label,
            float fontSize,
            Color textColor)
        {
            DrawGradientRoundedBox(g, bounds, 8, topColor, bottomColor, borderColor, 1.5f);
            using Font btnFont = new("Segoe UI", fontSize, FontStyle.Bold);
            using SolidBrush textBrush = new(textColor);
            using StringFormat sf = CreateStringFormat();
            g.DrawString(label, btnFont, textBrush, bounds, sf);
        }

        public static void DrawDisabledButton(
            Graphics g,
            Rectangle bounds,
            int radius,
            string label,
            float fontSize)
        {
            DrawRoundedBox(g, bounds, radius, Color.FromArgb(32, 37, 48), Color.FromArgb(58, 66, 82), 1.5f);
            using Font btnFont = new("Segoe UI", fontSize, FontStyle.Bold);
            using SolidBrush mutedBrush = new(UiTheme.Muted);
            using StringFormat sf = CreateStringFormat();
            g.DrawString(label, btnFont, mutedBrush, bounds, sf);
        }

        public static Color GetCategoryColor(CardCategory category) => category switch
        {
            CardCategory.Melee => Color.FromArgb(192, 58, 58),
            CardCategory.Ranged => Color.FromArgb(52, 152, 88),
            CardCategory.Movement => Color.FromArgb(52, 122, 194),
            CardCategory.Special => UiTheme.Purple,
            _ => Color.FromArgb(90, 100, 120)
        };

        public static Color DarkenColor(Color c, float factor) => Color.FromArgb(
            c.A,
            Math.Clamp((int)(c.R * factor), 0, 255),
            Math.Clamp((int)(c.G * factor), 0, 255),
            Math.Clamp((int)(c.B * factor), 0, 255));

        public static StringFormat CreateStringFormat(
            StringAlignment alignment = StringAlignment.Center,
            StringAlignment lineAlignment = StringAlignment.Center)
        {
            StringFormat format = new();
            format.Alignment = alignment;
            format.LineAlignment = lineAlignment;
            return format;
        }

        public static T HitTest<T>(List<T> hitboxes, Point point, Func<T, Rectangle> boundsSelector)
            where T : class
        {
            for (int i = hitboxes.Count - 1; i >= 0; i--)
            {
                if (boundsSelector(hitboxes[i]).Contains(point))
                {
                    return hitboxes[i];
                }
            }

            return null;
        }
    }
}