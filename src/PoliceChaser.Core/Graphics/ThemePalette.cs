using Microsoft.Xna.Framework;

namespace PoliceChaser.Graphics
{
    /// <summary>Colour schemes for each mission theme. Deliberately limited palettes.</summary>
    public struct ThemePalette
    {
        public Color SkyTop;
        public Color SkyBottom;
        public Color Sun;
        public Color FarSkyline;
        public Color NearSkyline;
        public Color Grass;
        public Color Road;
        public Color RoadDark;
        public Color RumbleLight;
        public Color RumbleDark;
        public Color Lane;
        public Color Fog;
        public Color BuildingLit;
        public Color BuildingDark;

        public static ThemePalette For(Game.MissionTheme theme)
        {
            switch (theme)
            {
                case Game.MissionTheme.Coast:
                    return new ThemePalette
                    {
                        SkyTop = new Color(28, 74, 156),
                        SkyBottom = new Color(246, 156, 92),
                        Sun = new Color(255, 236, 168),
                        FarSkyline = new Color(78, 94, 146),
                        NearSkyline = new Color(46, 56, 92),
                        Grass = new Color(52, 118, 70),
                        Road = new Color(74, 76, 86),
                        RoadDark = new Color(66, 68, 78),
                        RumbleLight = new Color(238, 238, 224),
                        RumbleDark = new Color(196, 76, 68),
                        Lane = new Color(232, 226, 184),
                        Fog = new Color(238, 166, 128),
                        BuildingLit = new Color(255, 214, 150),
                        BuildingDark = new Color(42, 50, 82)
                    };

                case Game.MissionTheme.Alpine:
                    return new ThemePalette
                    {
                        SkyTop = new Color(66, 104, 158),
                        SkyBottom = new Color(206, 224, 238),
                        Sun = new Color(250, 250, 236),
                        FarSkyline = new Color(150, 168, 190),
                        NearSkyline = new Color(96, 118, 146),
                        Grass = new Color(232, 238, 246),
                        Road = new Color(78, 84, 96),
                        RoadDark = new Color(70, 76, 88),
                        RumbleLight = new Color(240, 244, 250),
                        RumbleDark = new Color(150, 66, 66),
                        Lane = new Color(240, 238, 220),
                        Fog = new Color(210, 224, 238),
                        BuildingLit = new Color(246, 226, 160),
                        BuildingDark = new Color(70, 82, 104)
                    };

                case Game.MissionTheme.Neon:
                    return new ThemePalette
                    {
                        SkyTop = new Color(16, 8, 44),
                        SkyBottom = new Color(96, 24, 110),
                        Sun = new Color(255, 96, 190),
                        FarSkyline = new Color(60, 26, 92),
                        NearSkyline = new Color(30, 14, 54),
                        Grass = new Color(18, 18, 40),
                        Road = new Color(40, 38, 62),
                        RoadDark = new Color(34, 32, 54),
                        RumbleLight = new Color(96, 240, 240),
                        RumbleDark = new Color(240, 64, 160),
                        Lane = new Color(250, 230, 120),
                        Fog = new Color(84, 30, 108),
                        BuildingLit = new Color(110, 246, 236),
                        BuildingDark = new Color(20, 12, 40)
                    };

                case Game.MissionTheme.Desert:
                    return new ThemePalette
                    {
                        SkyTop = new Color(60, 120, 178),
                        SkyBottom = new Color(238, 196, 126),
                        Sun = new Color(255, 250, 210),
                        FarSkyline = new Color(176, 132, 96),
                        NearSkyline = new Color(128, 92, 66),
                        Grass = new Color(214, 176, 112),
                        Road = new Color(78, 74, 78),
                        RoadDark = new Color(70, 66, 70),
                        RumbleLight = new Color(242, 226, 186),
                        RumbleDark = new Color(176, 92, 62),
                        Lane = new Color(250, 240, 206),
                        Fog = new Color(238, 206, 150),
                        BuildingLit = new Color(246, 220, 160),
                        BuildingDark = new Color(120, 88, 62)
                    };

                case Game.MissionTheme.Arctic:
                    return new ThemePalette
                    {
                        SkyTop = new Color(18, 34, 68),
                        SkyBottom = new Color(122, 162, 196),
                        Sun = new Color(226, 240, 250),
                        FarSkyline = new Color(112, 146, 178),
                        NearSkyline = new Color(62, 90, 122),
                        Grass = new Color(214, 226, 238),
                        Road = new Color(70, 78, 92),
                        RoadDark = new Color(62, 70, 84),
                        RumbleLight = new Color(230, 238, 248),
                        RumbleDark = new Color(120, 64, 70),
                        Lane = new Color(236, 240, 246),
                        Fog = new Color(150, 182, 210),
                        BuildingLit = new Color(236, 226, 186),
                        BuildingDark = new Color(46, 66, 92)
                    };

                default:
                    return new ThemePalette
                    {
                        SkyTop = new Color(22, 40, 96),
                        SkyBottom = new Color(96, 132, 178),
                        Sun = new Color(250, 240, 200),
                        FarSkyline = new Color(52, 66, 104),
                        NearSkyline = new Color(30, 38, 64),
                        Grass = new Color(42, 92, 58),
                        Road = new Color(70, 72, 82),
                        RoadDark = new Color(62, 64, 74),
                        RumbleLight = new Color(232, 232, 220),
                        RumbleDark = new Color(188, 72, 66),
                        Lane = new Color(230, 224, 190),
                        Fog = new Color(96, 132, 178),
                        BuildingLit = new Color(255, 220, 150),
                        BuildingDark = new Color(34, 44, 74)
                    };
            }
        }
    }
}
