using System.Drawing;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public static class UiTheme
    {
        public const int WindowWidth = 1280;
        public const int WindowHeight = 720;
        public static readonly Color BgTop = Color.FromArgb(18, 22, 32);
        public static readonly Color BgBottom = Color.FromArgb(11, 14, 21);
        public static readonly Color HeaderBg = Color.FromArgb(24, 30, 44);
        public static readonly Color PanelBg = Color.FromArgb(28, 35, 50);
        public static readonly Color PanelBgAlt = Color.FromArgb(35, 43, 62);
        public static readonly Color CardBg = Color.FromArgb(24, 30, 43);
        public static readonly Color CardDimBg = Color.FromArgb(20, 24, 34);
        public static readonly Color BorderSubtle = Color.FromArgb(58, 70, 96);
        public static readonly Color BorderHighlight = Color.FromArgb(116, 168, 246);
        public static readonly Color BorderGold = Color.FromArgb(228, 186, 86);

        public static readonly Color TextPrimary = Color.FromArgb(240, 244, 252);
        public static readonly Color TextSecondary = Color.FromArgb(174, 188, 214);
        public static readonly Color TextMuted = Color.FromArgb(124, 138, 164);

        public static readonly Color AccentGold = Color.FromArgb(242, 198, 92);
        public static readonly Color AccentBlueTop = Color.FromArgb(56, 108, 186);
        public static readonly Color AccentBlueBottom = Color.FromArgb(38, 74, 138);
        public static readonly Color AccentGreenTop = Color.FromArgb(46, 138, 92);
        public static readonly Color AccentGreenBottom = Color.FromArgb(30, 98, 64);
        public static readonly Color AccentRedTop = Color.FromArgb(176, 56, 66);
        public static readonly Color AccentRedBottom = Color.FromArgb(126, 36, 46);
        public static readonly Color AccentPurpleTop = Color.FromArgb(124, 72, 178);
        public static readonly Color AccentPurpleBottom = Color.FromArgb(84, 46, 132);
        public static readonly Color AccentSlateTop = Color.FromArgb(52, 62, 84);
        public static readonly Color AccentSlateBottom = Color.FromArgb(36, 44, 62);

        public static readonly Color MeleeHeader = Color.FromArgb(176, 54, 64);
        public static readonly Color RangedHeader = Color.FromArgb(44, 142, 88);
        public static readonly Color MovementHeader = Color.FromArgb(48, 114, 192);
        public static readonly Color SpecialHeader = Color.FromArgb(132, 68, 184);

        public static readonly Color HpBarFill = Color.FromArgb(216, 68, 76);
        public static readonly Color HpBarBg = Color.FromArgb(46, 24, 28);
        public static readonly Color ShieldCyan = Color.FromArgb(92, 216, 242);
        public static readonly Color PoisonGreen = Color.FromArgb(94, 212, 98);
        public static readonly Color WarningBg = Color.FromArgb(118, 62, 24);
        public static readonly Color WarningBorder = Color.FromArgb(236, 168, 66);

        public static readonly Font FontTitle = new Font("Segoe UI", 22f, FontStyle.Bold);
        public static readonly Font FontHero = new Font("Segoe UI", 32f, FontStyle.Bold);
        public static readonly Font FontSection = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font FontCardTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        public static readonly Font FontBody = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font FontBodyBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font FontSmall = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font FontSmallBold = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        public static readonly Font FontBadge = new Font("Segoe UI", 8f, FontStyle.Bold);

        public static readonly StringFormat CenterFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        public static readonly StringFormat LeftCenterFormat = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };

        public static readonly StringFormat RightCenterFormat = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center
        };

        public static readonly StringFormat TopLeftWrapFormat = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisWord
        };

        public static Color GetCategoryColor(WeaponCategory category)
        {
            return category switch
            {
                WeaponCategory.Melee => MeleeHeader,
                WeaponCategory.Ranged => RangedHeader,
                WeaponCategory.Movement => MovementHeader,
                WeaponCategory.Special => SpecialHeader,
                _ => AccentSlateTop
            };
        }
    }
}