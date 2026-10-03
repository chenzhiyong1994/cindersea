using UnityEngine;

namespace Dicebound.Presentation
{
    /// <summary>Design tokens for the tactical edition; archived dice UI retains its own palette.</summary>
    public static class TacticalTheme
    {
        public static readonly Color Paper = new Color(.91f,.87f,.77f);
        public static readonly Color PaperLight = new Color(.98f,.95f,.87f);
        public static readonly Color Ink = new Color(.14f,.23f,.24f);
        public static readonly Color FadedInk = new Color(.23f,.29f,.28f);
        public static readonly Color OnDark = PaperLight;
        public static readonly Color OnDarkSub = new Color(.73f,.81f,.80f);
        public static readonly Color Gold = new Color(.90f,.76f,.49f);
        public static readonly Color GoldDeep = new Color(.59f,.52f,.36f);
        public static readonly Color Vermilion = new Color(.65f,.27f,.20f);
        public static readonly Color Ally = new Color(.43f,.81f,.72f);
        public static readonly Color Enemy = new Color(.91f,.42f,.30f);
        public static readonly Color Danger = new Color(.95f,.55f,.42f);
        public static readonly Color SpBlue = new Color(.51f,.73f,.91f);
        public static readonly Color MoveCell = new Color(.30f,.62f,.66f);
        public static readonly Color SkillCell = new Color(.85f,.52f,.28f);
        public static readonly Color Navy = new Color(.045f,.12f,.15f);
        public static readonly Color NightWash = new Color(.025f,.075f,.085f,.55f);
        public static readonly Color SceneTint = new Color(.77f,.79f,.75f,1);
        public static readonly Color ModalShade = new Color(.025f,.075f,.09f,.83f);
        public static readonly Color BadgeBackdrop = new Color(.035f,.105f,.12f,.96f);
        public static readonly Color Inset = new Color(.025f,.085f,.10f,.47f);
        public static readonly Color JadeRim = new Color(.40f,.67f,.65f,.66f);
        public static readonly Color RuleLine = new Color(.59f,.48f,.31f,.55f);
        public static readonly Color ResourceEmpty = new Color(.28f,.31f,.35f,.70f);
        public static readonly Color SelectedMark = Vermilion;
        public static readonly Color SkinFrame = Color.white;
        public static readonly Color ButtonNormal = Color.white;
        public static readonly Color ButtonHover = new Color(1.15f,1.15f,1.15f,1);
        public static readonly Color ButtonPressed = new Color(.85f,.85f,.85f,1);
        public static readonly Color ButtonDisabled = new Color(.55f,.55f,.55f,.6f);
        // Battlefield furniture is thin, matte ink; scene lighting and actors carry the visual weight.
        public static readonly Color HudInk = new Color(.025f,.043f,.052f,1);
        public static readonly Color HudInset = new Color(.020f,.034f,.043f,1);
        public static readonly Color HudSelected = new Color(.060f,.082f,.087f,1);
        public static readonly Color HudRule = new Color(.62f,.56f,.39f,.50f);
        public static readonly Color HudSelectedRule = new Color(.88f,.74f,.46f,.86f);
        public static readonly Color HudButtonDisabled = new Color(.58f,.58f,.58f,1);
        public static readonly Color HudMeterTrack = new Color(.095f,.145f,.155f,1);
        public const float BattleIconSize=64, BattleSkillWidth=124, BattleSkillHeight=148,
            BattleSkillGap=10, BattleSkillRowGap=8, BattleSkinDensity=8;
        public const float BattleFrameNormal=.36f, BattleFrameSubtle=.20f,
            BattleFrameHover=.55f, BattleFrameSelected=.78f, BattleLiningOpacity=.12f,
            BattleToolbarHeight=44, BattleAllyHeight=184, BattleAllyGap=12;

        public const int FontBadge=14, FontLabel=16, FontSecondary=18, FontBody=20,
            FontButton=22, FontEmphasis=24, FontCard=28, FontSection=32,
            FontPage=40, FontModal=48;
        // Title-screen display type and its ceremonial spaced lettering are deliberate exceptions.
        public const int FontTitle=88, FontTitleSecondary=83;
        public const float TextLineHeight=1.5f;
        public const float Space4=4, Space8=8, Space12=12, Space16=16,
            Space24=24, Space32=32, Space48=48, Space64=64;
        public const float ButtonPrimaryHeight=68, ButtonSecondaryHeight=56,
            ButtonQuietHeight=48, HeaderButtonHeight=46;
        public const float ButtonMinIconWidth=240, ButtonMinTextWidth=120,
            ButtonIconSize=28, ButtonPaddingX=16, ButtonPaddingY=4,
            ButtonIconGap=12, ButtonFadeDuration=.10f,
            ButtonHoverScale=1.015f, ButtonPressedScale=.985f;
        public const float PanelPaddingLarge=48, PanelPaddingMedium=32,
            PanelPaddingCompact=24, CardGap=24;
        public const float PortraitBorder=4, MeterBorder=2,
            ResourcePipsWidth=100, ResourcePipStep=20, ResourcePipSize=18,
            ResourcePipGap=2;
        public static readonly Rect BoardSafeRect=Rect.MinMaxRect(-646,-344,646,466);
    }

    public enum TacticalButtonVariant { Primary, Secondary, Quiet }
    public enum TacticalSurfaceContext { Dark, Light }
}
