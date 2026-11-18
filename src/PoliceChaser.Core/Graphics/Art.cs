using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoliceChaser.Game;

namespace PoliceChaser.Graphics
{
    /// <summary>Tiny software canvas used to bake all of the game's pixel art at startup.</summary>
    public sealed class Canvas
    {
        /// <summary>Supersampling factor. Every canvas is authored in logical pixels and
        /// rasterised at this multiple so the art is genuinely high resolution.</summary>
        public static int DefaultScale = 1;

        public readonly Color[] Pixels;
        public readonly int Width;
        public readonly int Height;
        private readonly int _scale;

        public Canvas(int width, int height)
        {
            _scale = DefaultScale < 1 ? 1 : DefaultScale;
            Width = width * _scale;
            Height = height * _scale;
            Pixels = new Color[Width * Height];
        }

        private void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }

            Pixels[y * Width + x] = color;
        }

        private Color GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return default;
            }

            return Pixels[y * Width + x];
        }

        public void Clear(Color color)
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = color;
            }
        }

        public void Set(int x, int y, Color color)
        {
            int scale = _scale;
            for (int dy = 0; dy < scale; dy++)
            {
                for (int dx = 0; dx < scale; dx++)
                {
                    SetPixel(x * scale + dx, y * scale + dy, color);
                }
            }
        }

        public void Blend(int x, int y, Color color)
        {
            if (color.A == 0)
            {
                return;
            }

            int scale = _scale;
            for (int dy = 0; dy < scale; dy++)
            {
                for (int dx = 0; dx < scale; dx++)
                {
                    int px = x * scale + dx;
                    int py = y * scale + dy;
                    Color existing = GetPixel(px, py);
                    if (existing.A == 0)
                    {
                        SetPixel(px, py, color);
                        continue;
                    }

                    float a = color.A / 255f;
                    SetPixel(px, py, new Color(
                        (int)(existing.R * (1 - a) + color.R * a),
                        (int)(existing.G * (1 - a) + color.G * a),
                        (int)(existing.B * (1 - a) + color.B * a),
                        Math.Max(existing.A, color.A)));
                }
            }
        }

        public void FillRect(int x, int y, int w, int h, Color color)
        {
            int scale = _scale;
            int px = x * scale;
            int py = y * scale;
            int pw = w * scale;
            int ph = h * scale;
            for (int yy = py; yy < py + ph; yy++)
            {
                for (int xx = px; xx < px + pw; xx++)
                {
                    SetPixel(xx, yy, color);
                }
            }
        }

        private void SetDevice(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }

            Pixels[y * Width + x] = color;
        }

        /// <summary>Rounded rectangle rasterised at canvas resolution, so curved panels
        /// stay smooth once the canvas is downsampled into a vehicle texture.</summary>
        public void FillRoundedRect(int x, int y, int w, int h, int radius, Color color)
        {
            FillRoundedRectGradient(x, y, w, h, radius, color, color);
        }

        /// <summary>Rounded rectangle filled with a vertical gradient.</summary>
        public void FillRoundedRectGradient(int x, int y, int w, int h, int radius, Color top, Color bottom)
        {
            if (w <= 0 || h <= 0)
            {
                return;
            }

            int scale = _scale;
            int px = x * scale;
            int py = y * scale;
            int pw = w * scale;
            int ph = h * scale;
            int r = Math.Max(0, Math.Min(radius * scale, Math.Min(pw, ph) / 2));

            for (int row = 0; row < ph; row++)
            {
                float edge = 0f;
                if (row < r)
                {
                    edge = r - row;
                }
                else if (row > ph - 1 - r)
                {
                    edge = row - (ph - 1 - r);
                }

                int inset = edge > 0f
                    ? (int)MathF.Round(r - MathF.Sqrt(MathF.Max(0f, r * r - edge * edge)))
                    : 0;
                Color color = Color.Lerp(top, bottom, ph == 1 ? 0f : row / (float)(ph - 1));

                for (int xx = px + inset; xx < px + pw - inset; xx++)
                {
                    SetDevice(xx, py + row, color);
                }
            }
        }

        public void Rect(int x, int y, int w, int h, Color color)
        {
            int t = _scale;
            FillRect(x, y, w, 1, color);
            FillRect(x, y + h - 1, w, 1, color);
            FillRect(x, y, 1, h, color);
            FillRect(x + w - 1, y, 1, h, color);
            // Make the outline the same visual weight as the filled shapes.
            if (t > 1)
            {
                FillRect(x + 1, y + 1, w - 2, 1, color);
                FillRect(x + 1, y + h - 2, w - 2, 1, color);
            }
        }

        public void Line(int x0, int y0, int x1, int y1, Color color)
        {
            int scale = _scale;
            int ax = x0 * scale;
            int ay = y0 * scale;
            int bx = x1 * scale;
            int by = y1 * scale;
            int thickness = Math.Max(1, scale);
            int dx = Math.Abs(bx - ax);
            int dy = -Math.Abs(by - ay);
            int sx = ax < bx ? 1 : -1;
            int sy = ay < by ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                for (int ox = 0; ox < thickness; ox++)
                {
                    for (int oy = 0; oy < thickness; oy++)
                    {
                        SetPixel(ax + ox, ay + oy, color);
                    }
                }

                if (ax == bx && ay == by)
                {
                    break;
                }

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    ax += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    ay += sy;
                }
            }
        }

        public void FillEllipse(int cx, int cy, int rx, int ry, Color color)
        {
            int scale = _scale;
            int ccx = cx * scale;
            int ccy = cy * scale;
            int rrx = Math.Max(1, rx * scale);
            int rry = Math.Max(1, ry * scale);

            for (int y = -rry; y <= rry; y++)
            {
                for (int x = -rrx; x <= rrx; x++)
                {
                    float nx = x / (float)rrx;
                    float ny = y / (float)rry;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        SetDevice(ccx + x, ccy + y, color);
                    }
                }
            }
        }

        public void FillTrapezoid(int topY, int bottomY, int topLeft, int topRight, int bottomLeft, int bottomRight, Color color)
        {
            int scale = _scale;
            int ty = topY * scale;
            int by = bottomY * scale;
            int height = by - ty;
            if (height <= 0)
            {
                return;
            }

            for (int y = ty; y < by; y++)
            {
                float t = (y - ty) / (float)height;
                int left = (int)((topLeft + (bottomLeft - topLeft) * t) * scale);
                int right = (int)((topRight + (bottomRight - topRight) * t) * scale);
                for (int x = left; x <= right; x++)
                {
                    SetPixel(x, y, color);
                }
            }
        }

        public void FillPolygon(Point[] points, Color color)
        {
            if (points == null || points.Length < 3)
            {
                return;
            }

            int scale = _scale;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int i = 0; i < points.Length; i++)
            {
                minY = Math.Min(minY, points[i].Y * scale);
                maxY = Math.Max(maxY, points[i].Y * scale);
            }

            var crossings = new List<float>(8);
            for (int y = Math.Max(0, minY); y <= Math.Min(Height - 1, maxY); y++)
            {
                crossings.Clear();
                for (int i = 0; i < points.Length; i++)
                {
                    int ax = points[i].X * scale;
                    int ay = points[i].Y * scale;
                    int bx = points[(i + 1) % points.Length].X * scale;
                    int by = points[(i + 1) % points.Length].Y * scale;
                    if ((ay <= y && by > y) || (by <= y && ay > y))
                    {
                        float t = (y - ay) / (float)(by - ay);
                        crossings.Add(ax + t * (bx - ax));
                    }
                }

                crossings.Sort();
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    int left = (int)MathF.Round(crossings[i]);
                    int right = (int)MathF.Round(crossings[i + 1]);
                    for (int x = left; x <= right; x++)
                    {
                        SetPixel(x, y, color);
                    }
                }
            }
        }

        public Texture2D ToTexture(GraphicsDevice device)
        {
            var texture = new Texture2D(device, Width, Height);
            texture.SetData(Pixels);
            return texture;
        }

        /// <summary>
        /// Bakes this supersampled canvas down to a smaller texture with a box filter.
        /// Colours are averaged premultiplied so transparent corners do not darken
        /// the anti-aliased edge.
        /// </summary>
        public Texture2D ToTexture(GraphicsDevice device, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (width == Width && height == Height)
            {
                return ToTexture(device);
            }

            if (Width % width != 0 || Height % height != 0)
            {
                throw new ArgumentException("target size must be an integer divisor of the canvas");
            }

            int blockX = Width / width;
            int blockY = Height / height;
            float samples = blockX * blockY;
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float r = 0f, g = 0f, b = 0f, alpha = 0f;
                    for (int yy = 0; yy < blockY; yy++)
                    {
                        int row = (y * blockY + yy) * Width + x * blockX;
                        for (int xx = 0; xx < blockX; xx++)
                        {
                            Color pixel = Pixels[row + xx];
                            float coverage = pixel.A / 255f;
                            r += pixel.R * coverage;
                            g += pixel.G * coverage;
                            b += pixel.B * coverage;
                            alpha += coverage;
                        }
                    }

                    if (alpha <= 0.0001f)
                    {
                        pixels[y * width + x] = Color.Transparent;
                        continue;
                    }

                    float inverse = 1f / alpha;
                    pixels[y * width + x] = new Color(
                        (int)(r * inverse),
                        (int)(g * inverse),
                        (int)(b * inverse),
                        (int)(alpha * 255f / samples));
                }
            }

            var texture = new Texture2D(device, width, height);
            texture.SetData(pixels);
            return texture;
        }
    }

    /// <summary>
    /// Every sprite in the game is baked here. No third-party or original game artwork is used:
    /// the vehicles, scenery and logo are original interpretations drawn in an 8-bit style.
    /// </summary>
    public sealed class Art : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private readonly Dictionary<MissionTheme, ThemePalette> _palettes = new Dictionary<MissionTheme, ThemePalette>();
        private readonly Dictionary<MissionTheme, Texture2D> _skyBack = new Dictionary<MissionTheme, Texture2D>();
        private readonly Dictionary<MissionTheme, Texture2D> _skyFront = new Dictionary<MissionTheme, Texture2D>();
        private readonly Dictionary<MissionTheme, Texture2D> _sun = new Dictionary<MissionTheme, Texture2D>();
        private readonly Dictionary<MissionTheme, Texture2D> _rain = new Dictionary<MissionTheme, Texture2D>();

        public Texture2D White { get; }
        public Texture2D PlayerCar { get; }
        public Texture2D PlayerCarLeft { get; }
        public Texture2D PlayerCarRight { get; }
        public Texture2D Shadow { get; }
        public Texture2D[] SuspectCars { get; }
        public Texture2D[] TrafficCars { get; }
        public Texture2D[] OncomingCars { get; }
        public Texture2D Badge { get; }
        public Texture2D SplashArt { get; }
        public Texture2D[] HqOperatorFrames { get; }
        public Texture2D PlayerPortrait { get; }

        private static readonly Color Blue = new Color(38, 78, 196);
        private static readonly Color Red = new Color(216, 48, 48);
        private static readonly Color Yellow = new Color(250, 210, 80);
        private static readonly Color WhiteColor = new Color(244, 244, 250);
        private static readonly Color Dark = new Color(26, 28, 40);
        private static readonly Color Glass = new Color(44, 62, 96);
        private static readonly Color Tyre = new Color(18, 18, 24);

        public Art(GraphicsDevice device)
        {
            _device = device;
            Canvas.DefaultScale = RenderSettings.Scale;

            var white = new Canvas(1, 1);
            white.Set(0, 0, Color.White);
            White = white.ToTexture(device);

            PlayerCar = BuildPlayerCar(0);
            PlayerCarLeft = BuildPlayerCar(-1);
            PlayerCarRight = BuildPlayerCar(1);

            Shadow = BuildShadow();

            SuspectCars = new Texture2D[6];
            var suspectColors = new[]
            {
                new Color(178, 34, 52),
                new Color(226, 132, 30),
                new Color(96, 106, 124),
                new Color(150, 44, 196),
                new Color(196, 150, 44),
                new Color(38, 148, 168)
            };

            for (int i = 0; i < SuspectCars.Length; i++)
            {
                SuspectCars[i] = BuildCar(56, 38, suspectColors[i], true, false);
            }

            TrafficCars = new Texture2D[4];
            TrafficCars[0] = BuildCar(52, 36, new Color(214, 214, 220), false, false);
            TrafficCars[1] = BuildCar(52, 40, new Color(80, 128, 200), false, true);
            TrafficCars[2] = BuildCar(60, 46, new Color(200, 180, 90), false, true);
            TrafficCars[3] = BuildCar(48, 34, new Color(70, 160, 110), false, false);

            OncomingCars = new Texture2D[4];
            for (int i = 0; i < 4; i++)
            {
                OncomingCars[i] = BuildCar(52, 36, i == 1 ? new Color(180, 90, 70) : new Color(190, 194, 206), false, false, true);
            }

            Badge = BuildBadge();
            SplashArt = BuildSplashArt();
            HqOperatorFrames = new Texture2D[4];
            for (int i = 0; i < HqOperatorFrames.Length; i++)
            {
                HqOperatorFrames[i] = BuildHqOperator(i);
            }

            PlayerPortrait = BuildPlayerPortrait();
        }

        public ThemePalette Palette(MissionTheme theme)
        {
            if (!_palettes.TryGetValue(theme, out ThemePalette palette))
            {
                palette = ThemePalette.For(theme);
                _palettes[theme] = palette;
            }

            return palette;
        }

        public Texture2D Prop(PropKind kind, int variant)
        {
            string key = "prop:" + kind + ":" + variant;
            if (_cache.TryGetValue(key, out Texture2D cached))
            {
                return cached;
            }

            Canvas canvas = BuildProp(kind, variant);
            Texture2D texture = canvas.ToTexture(_device);
            _cache[key] = texture;
            return texture;
        }

        /// <summary>World width used to scale a prop sprite correctly against the road.</summary>
        public static float PropWorldWidth(PropKind kind)
        {
            switch (kind)
            {
                case PropKind.Palm: return 1000f;
                case PropKind.Pine: return 900f;
                case PropKind.Cactus: return 700f;
                case PropKind.Rock: return 1000f;
                case PropKind.Lamp: return 400f;
                case PropKind.Sign: return 700f;
                case PropKind.SnowPole: return 300f;
                case PropKind.Building: return 2300f;
                case PropKind.Billboard: return 2200f;
                default: return 900f;
            }
        }

        public Texture2D Skyline(MissionTheme theme, bool front)
        {
            Dictionary<MissionTheme, Texture2D> map = front ? _skyFront : _skyBack;
            if (map.TryGetValue(theme, out Texture2D texture))
            {
                return texture;
            }

            ThemePalette palette = Palette(theme);
            var canvas = new Canvas(640, front ? 96 : 72);
            var random = new Random(9001 + (int)theme * 37 + (front ? 11 : 0));
            Color color = front ? palette.NearSkyline : palette.FarSkyline;

            if (theme == MissionTheme.Alpine || theme == MissionTheme.Coast || theme == MissionTheme.Highway)
            {
                // Mountain range.
                int baseY = canvas.Height;
                int peakCount = front ? 6 : 9;
                for (int p = 0; p < peakCount; p++)
                {
                    int cx = (p * canvas.Width / peakCount) + random.Next(-20, 20);
                    int halfWidth = 70 + random.Next(70);
                    int height = (front ? 62 : 44) + random.Next(30);
                    for (int x = cx - halfWidth; x <= cx + halfWidth; x++)
                    {
                        float t = Math.Abs(x - cx) / (float)halfWidth;
                        int top = baseY - (int)(height * (1f - t * t));
                        for (int y = top; y < baseY; y++)
                        {
                            canvas.Set(x, y, color);
                        }
                    }

                    if (theme == MissionTheme.Alpine)
                    {
                        // Snow caps.
                        for (int x = cx - halfWidth / 4; x <= cx + halfWidth / 4; x++)
                        {
                            float t = Math.Abs(x - cx) / (halfWidth / 4f);
                            int top = baseY - (int)(height * (1f - 0.25f * t * t));
                            for (int y = top; y < top + 12; y++)
                            {
                                canvas.Set(x, y, new Color(238, 244, 252));
                            }
                        }
                    }
                }
            }
            else if (theme == MissionTheme.Desert || theme == MissionTheme.Canyon)
            {
                // Mesa shapes.
                for (int p = 0; p < 8; p++)
                {
                    int x = p * 84 + random.Next(-12, 12);
                    int w = 60 + random.Next(50);
                    int h = 22 + random.Next(34);
                    canvas.FillRect(x, canvas.Height - h, w, h, color);
                    canvas.FillRect(x + 8, canvas.Height - h - 8, w - 16, 10, color);
                }
            }
            else
            {
                // City blocks.
                int cursor = 0;
                while (cursor < canvas.Width)
                {
                    int w = 24 + random.Next(40);
                    int h = 18 + random.Next(front ? 70 : 50);
                    int top = canvas.Height - h;
                    canvas.FillRect(cursor, top, w, h, color);

                    for (int wy = top + 4; wy < canvas.Height - 4; wy += 7)
                    {
                        for (int wx = cursor + 3; wx < cursor + w - 3; wx += 6)
                        {
                            if (random.Next(100) < 42)
                            {
                                canvas.Set(wx, wy, theme == MissionTheme.Neon
                                    ? (random.Next(2) == 0 ? palette.BuildingLit : new Color(250, 120, 200))
                                    : palette.BuildingLit);
                            }
                        }
                    }

                    cursor += w + random.Next(2, 14);
                }
            }

            Texture2D result = canvas.ToTexture(_device);
            map[theme] = result;
            return result;
        }

        public Texture2D Sun(MissionTheme theme)
        {
            if (_sun.TryGetValue(theme, out Texture2D texture))
            {
                return texture;
            }

            ThemePalette palette = Palette(theme);
            var canvas = new Canvas(64, 64);
            int r = theme == MissionTheme.Neon ? 20 : 26;
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    float d = MathF.Sqrt(x * x + y * y);
                    if (d <= r)
                    {
                        Color c = palette.Sun;
                        if (theme == MissionTheme.Neon)
                        {
                            // Synthwave sun: banded lower half.
                            if (y > 0 && (y / 4) % 2 == 0 && y > 2)
                            {
                                continue;
                            }

                            c = new Color(255, 120, 180);
                        }

                        canvas.Set(x + 32, y + 32, c);
                    }
                    else if (d <= r + 2)
                    {
                        canvas.Blend(x + 32, y + 32, new Color(255, 255, 255, 40));
                    }
                }
            }

            texture = canvas.ToTexture(_device);
            _sun[theme] = texture;
            return texture;
        }

        public Texture2D RainDrop()
        {
            const string key = "raindrop";
            if (_cache.TryGetValue(key, out Texture2D cached))
            {
                return cached;
            }

            var canvas = new Canvas(3, 10);
            canvas.FillRect(1, 0, 1, 7, new Color(190, 214, 255, 180));
            canvas.FillRect(0, 6, 3, 2, new Color(190, 214, 255, 140));
            Texture2D texture = canvas.ToTexture(_device);
            _cache[key] = texture;
            return texture;
        }

        public Texture2D SnowFlake()
        {
            const string key = "snowflake";
            if (_cache.TryGetValue(key, out Texture2D cached))
            {
                return cached;
            }

            var canvas = new Canvas(3, 3);
            canvas.Set(1, 0, new Color(255, 255, 255, 230));
            canvas.Set(0, 1, new Color(255, 255, 255, 230));
            canvas.Set(1, 1, new Color(255, 255, 255, 240));
            canvas.Set(2, 1, new Color(255, 255, 255, 230));
            canvas.Set(1, 2, new Color(255, 255, 255, 200));
            Texture2D texture = canvas.ToTexture(_device);
            _cache[key] = texture;
            return texture;
        }

        /// <summary>
        /// Bakes a vehicle at twice the canvas resolution and averages it back down, so
        /// rounded panels and tyres are anti-aliased instead of stair-stepped.
        /// </summary>
        private Texture2D BakeSmooth(int width, int height, Action<Canvas> draw)
        {
            int scale = RenderSettings.Scale;
            Canvas.DefaultScale = scale * 2;
            Canvas canvas;
            try
            {
                canvas = new Canvas(width, height);
                draw(canvas);
            }
            finally
            {
                Canvas.DefaultScale = scale;
            }

            return canvas.ToTexture(_device, width * scale, height * scale);
        }

        private static Color Lighten(Color color, float amount)
        {
            return new Color(
                (int)MathF.Min(255f, color.R + (255 - color.R) * amount),
                (int)MathF.Min(255f, color.G + (255 - color.G) * amount),
                (int)MathF.Min(255f, color.B + (255 - color.B) * amount));
        }

        private static Color Darken(Color color, float amount)
        {
            return new Color(
                (int)(color.R * (1f - amount)),
                (int)(color.G * (1f - amount)),
                (int)(color.B * (1f - amount)));
        }

        private Texture2D BuildPlayerCar(int lean)
        {
            return BakeSmooth(64, 44, canvas =>
            {
                int bias = lean * 2;

                // Tyres, with a subtle hub highlight.
                canvas.FillEllipse(11 + bias, 33, 7, 10, Tyre);
                canvas.FillEllipse(53 + bias, 33, 7, 10, Tyre);
                canvas.FillEllipse(11 + bias, 34, 3, 5, new Color(56, 60, 74));
                canvas.FillEllipse(53 + bias, 34, 3, 5, new Color(56, 60, 74));

                // Cabin and rear window.
                canvas.FillRoundedRect(17 + bias, 3, 30, 21, 7, Dark);
                canvas.FillRoundedRectGradient(20 + bias, 6, 24, 15, 5,
                    new Color(96, 124, 170), Glass);
                canvas.FillRoundedRect(22 + bias, 7, 20, 4, 2, new Color(176, 204, 238, 120));

                // Body shell with a soft top-to-bottom sheen.
                canvas.FillRoundedRectGradient(3, 18, 58, 22, 6,
                    WhiteColor, new Color(202, 206, 222));
                canvas.FillRoundedRect(5, 19, 54, 3, 1, new Color(255, 255, 255, 130));

                // Tail lights.
                canvas.FillRoundedRect(5, 21, 11, 7, 2, Red);
                canvas.FillRoundedRect(48, 21, 11, 7, 2, Red);
                canvas.FillRoundedRect(6, 22, 9, 2, 1, new Color(255, 154, 150, 180));
                canvas.FillRoundedRect(49, 22, 9, 2, 1, new Color(255, 154, 150, 180));

                // Livery.
                canvas.FillRoundedRect(4, 28, 56, 6, 2, Blue);
                canvas.FillRoundedRect(4, 33, 56, 2, 1, new Color(24, 52, 140));
                for (int i = 0; i < 7; i++)
                {
                    if (i % 2 == 0)
                    {
                        canvas.FillRect(9 + i * 7, 35, 4, 2, Blue);
                    }
                }

                // Bumper and plate.
                canvas.FillRoundedRect(4, 37, 56, 5, 2, new Color(52, 54, 66));
                canvas.FillRoundedRect(26, 38, 12, 3, 1, new Color(200, 200, 208));

                // Light bar.
                canvas.FillRoundedRect(19 + bias, 0, 26, 5, 2, Dark);
                canvas.FillRoundedRect(20 + bias, 1, 11, 3, 1, Red);
                canvas.FillRoundedRect(33 + bias, 1, 11, 3, 1, new Color(70, 140, 255));
                canvas.FillRoundedRect(20 + bias, 1, 11, 1, 1, new Color(255, 160, 160));
                canvas.FillRoundedRect(33 + bias, 1, 11, 1, 1, new Color(170, 210, 255));
            });
        }

        private Texture2D BuildShadow()
        {
            return BakeSmooth(64, 14, canvas =>
            {
                canvas.FillEllipse(32, 7, 30, 6, new Color(0, 0, 0, 90));
                canvas.FillEllipse(32, 7, 22, 4, new Color(0, 0, 0, 140));
            });
        }

        private Texture2D BuildCar(int width, int height, Color body, bool sports, bool tall, bool oncoming = false)
        {
            return BakeSmooth(width, height, canvas =>
            {
                int bodyTop = tall ? 12 : 10;
                int bodyBottom = height - 5;

                // Wheels first; the body shell tucks over their tops.
                canvas.FillEllipse(7, height - 9, 5, 8, Tyre);
                canvas.FillEllipse(width - 7, height - 9, 5, 8, Tyre);

                Color shellTop = Lighten(body, 0.14f);
                Color shellBottom = Darken(body, 0.24f);

                // Cabin and window.
                canvas.FillRoundedRect(7, 2, width - 14, bodyTop + 5, 5, Darken(body, 0.34f));
                canvas.FillRoundedRectGradient(9, 4, width - 18, bodyTop, 4,
                    oncoming ? new Color(74, 96, 132) : new Color(58, 78, 112),
                    oncoming ? new Color(44, 58, 84) : new Color(34, 48, 72));
                canvas.FillRoundedRect(11, 5, width - 22, 2, 1, new Color(190, 214, 245, 90));

                // Body shell.
                canvas.FillRoundedRectGradient(2, bodyTop, width - 4, bodyBottom - bodyTop, 5,
                    shellTop, shellBottom);
                canvas.FillRoundedRect(3, bodyTop + 1, width - 6, 2, 1, new Color(255, 255, 255, 75));

                if (sports)
                {
                    // Rear wing.
                    canvas.FillRoundedRect(5, bodyTop + 1, width - 10, 3, 2, new Color(20, 22, 34));
                    canvas.FillRect(8, bodyTop + 4, 3, 4, new Color(20, 22, 34));
                    canvas.FillRect(width - 11, bodyTop + 4, 3, 4, new Color(20, 22, 34));
                }

                if (oncoming)
                {
                    // Headlights and grille.
                    canvas.FillRoundedRect(4, bodyTop + 8, 10, 6, 2, new Color(255, 250, 214));
                    canvas.FillRoundedRect(width - 14, bodyTop + 8, 10, 6, 2, new Color(255, 250, 214));
                    canvas.FillRoundedRect(6, bodyTop + 9, 6, 3, 1, Color.White);
                    canvas.FillRoundedRect(width - 12, bodyTop + 9, 6, 3, 1, Color.White);
                    canvas.FillRoundedRect(width / 2 - 8, bodyTop + 11, 16, 5, 2, new Color(40, 42, 52));
                }
                else
                {
                    // Tail lights.
                    canvas.FillRoundedRect(4, bodyTop + 9, 9, 6, 2, new Color(220, 44, 40));
                    canvas.FillRoundedRect(width - 13, bodyTop + 9, 9, 6, 2, new Color(220, 44, 40));
                    canvas.FillRoundedRect(5, bodyTop + 10, 7, 2, 1, new Color(255, 150, 130, 180));
                    canvas.FillRoundedRect(width - 12, bodyTop + 10, 7, 2, 1, new Color(255, 150, 130, 180));
                }

                // Bumper.
                canvas.FillRoundedRect(2, height - 7, width - 4, 5, 2, new Color(48, 50, 62));
            });
        }

        /// <summary>HQ dispatcher at the other end of the radio, in the style of the 8-bit original.</summary>
        private Texture2D BuildHqOperator(int frame)
        {
            var canvas = new Canvas(26, 18);
            canvas.Clear(new Color(16, 22, 46));

            // Shoulders and collar.
            canvas.FillRect(2, 14, 22, 4, new Color(44, 92, 200));
            canvas.FillRect(9, 13, 8, 2, new Color(226, 232, 244));

            // Face and blonde hair.
            canvas.FillRect(8, 4, 10, 10, new Color(236, 196, 158));
            canvas.FillRect(6, 2, 14, 4, new Color(246, 206, 96));
            canvas.FillRect(5, 5, 3, 7, new Color(246, 206, 96));
            canvas.FillRect(18, 5, 3, 7, new Color(246, 206, 96));

            // Headset band, earpiece, boom mic.
            canvas.FillRect(7, 1, 12, 2, new Color(28, 32, 46));
            canvas.FillRect(5, 6, 3, 4, new Color(28, 32, 46));
            canvas.FillRect(18, 6, 3, 4, new Color(28, 32, 46));
            canvas.FillRect(9, 12, 6, 1, new Color(28, 32, 46));
            canvas.FillRect(9, 11, 2, 2, new Color(28, 32, 46));
            // Frame 0 is idle; 1-3 are the talking loop, with the mic LED blinking on frame 2.
            canvas.Set(11, 12,
                frame == 2 ? new Color(90, 40, 50) :
                frame > 0 ? new Color(255, 96, 80) : new Color(150, 50, 60));

            switch (frame)
            {
                case 1:
                    canvas.FillRect(11, 10, 4, 2, new Color(96, 44, 44));
                    canvas.FillRect(12, 10, 2, 1, new Color(242, 236, 230));
                    break;
                case 2:
                    canvas.FillRect(11, 11, 4, 1, new Color(120, 60, 60));
                    break;
                case 3:
                    canvas.FillRect(10, 10, 6, 3, new Color(96, 44, 44));
                    canvas.FillRect(11, 10, 4, 1, new Color(242, 236, 230));
                    break;
                default:
                    canvas.FillRect(11, 11, 4, 1, new Color(172, 96, 84));
                    break;
            }

            // Eyes.
            canvas.Set(10, 8, new Color(28, 34, 52));
            canvas.Set(15, 8, new Color(28, 34, 52));

            return canvas.ToTexture(_device);
        }

        /// <summary>The player, as seen in the original's video link: cap, badge, blue uniform.</summary>
        private Texture2D BuildPlayerPortrait()
        {
            var canvas = new Canvas(26, 18);
            canvas.Clear(new Color(12, 18, 40));

            // Uniform and collar.
            canvas.FillRect(2, 14, 22, 4, new Color(32, 64, 150));
            canvas.FillRect(9, 13, 8, 2, new Color(210, 220, 240));
            canvas.Set(4, 15, new Color(240, 200, 90));

            // Face.
            canvas.FillRect(8, 6, 10, 9, new Color(226, 182, 142));

            // Cap with a peaked brim and badge.
            canvas.FillRect(6, 2, 14, 4, new Color(32, 64, 150));
            canvas.FillRect(4, 5, 18, 2, new Color(24, 48, 120));
            canvas.FillRect(12, 2, 3, 2, new Color(240, 200, 90));

            // Eyes and jaw shadow.
            canvas.Set(10, 9, new Color(28, 34, 52));
            canvas.Set(15, 9, new Color(28, 34, 52));
            canvas.FillRect(11, 12, 4, 1, new Color(150, 100, 80));

            return canvas.ToTexture(_device);
        }

        private Texture2D BuildBadge()
        {
            var canvas = new Canvas(32, 36);
            Color shield = new Color(28, 62, 164);
            Color edge = new Color(240, 200, 90);

            var outline = new[]
            {
                new Point(16, 0), new Point(31, 5), new Point(31, 23),
                new Point(16, 35), new Point(1, 23), new Point(1, 5)
            };
            canvas.FillPolygon(outline, edge);

            var inner = new[]
            {
                new Point(16, 3), new Point(28, 7), new Point(28, 21),
                new Point(16, 31), new Point(4, 21), new Point(4, 7)
            };
            canvas.FillPolygon(inner, shield);

            var star = new Point[10];
            for (int i = 0; i < 10; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                float radius = i % 2 == 0 ? 9f : 3.8f;
                star[i] = new Point(
                    16 + (int)MathF.Round(MathF.Cos((float)angle) * radius),
                    15 + (int)MathF.Round(MathF.Sin((float)angle) * radius));
            }

            canvas.FillPolygon(star, edge);
            return canvas.ToTexture(_device);
        }

        private Texture2D BuildSplashArt()
        {
            var canvas = new Canvas(160, 96);
            Color sky = new Color(14, 20, 44);
            canvas.Clear(sky);

            // Siren glow.
            for (int y = 0; y < 96; y++)
            {
                float t = y / 95f;
                canvas.FillRect(0, y, 160, 1, new Color(
                    (int)(14 + 30 * t),
                    (int)(20 + 34 * t),
                    (int)(44 + 46 * t)));
            }

            // Road.
            for (int y = 60; y < 96; y++)
            {
                float t = (y - 60) / 36f;
                int half = (int)(6 + 52 * t);
                canvas.FillRect(80 - half, y, half * 2, 1, new Color(40, 42, 56));
                canvas.FillRect(80 - half, y, 2, 1, new Color(220, 220, 230));
                canvas.FillRect(80 + half - 2, y, 2, 1, new Color(220, 220, 230));
            }

            // Road stripes.
            for (int i = 0; i < 5; i++)
            {
                int y = 62 + i * 8;
                float t = (y - 60) / 36f;
                int half = (int)(1 + 6 * t);
                canvas.FillRect(80 - half, y, half * 2, 2, new Color(230, 224, 180));
            }

            return canvas.ToTexture(_device);
        }

        private Canvas BuildProp(PropKind kind, int variant)
        {
            switch (kind)
            {
                case PropKind.Palm:
                {
                    var c = new Canvas(28, 72);
                    Color trunk = new Color(120, 86, 52);
                    for (int y = 20; y < 72; y++)
                    {
                        int x = 13 + (int)(MathF.Sin(y * 0.12f) * 2.2f);
                        c.Set(x, y, trunk);
                        c.Set(x + 1, y, trunk);
                    }

                    Color frond = variant % 2 == 0 ? new Color(56, 140, 74) : new Color(44, 118, 64);
                    c.FillEllipse(14, 16, 13, 5, frond);
                    c.FillEllipse(14, 16, 5, 13, frond);
                    c.FillEllipse(8, 10, 7, 4, frond);
                    c.FillEllipse(20, 10, 7, 4, frond);
                    c.FillEllipse(8, 22, 7, 4, frond);
                    c.FillEllipse(20, 22, 7, 4, frond);
                    return c;
                }

                case PropKind.Pine:
                {
                    var c = new Canvas(26, 58);
                    Color leaf = variant % 2 == 0 ? new Color(36, 96, 60) : new Color(28, 78, 50);
                    c.FillRect(11, 44, 4, 14, new Color(90, 66, 44));
                    for (int layer = 0; layer < 3; layer++)
                    {
                        int top = 6 + layer * 13;
                        for (int y = top; y < top + 18; y++)
                        {
                            float t = (y - top) / 18f;
                            int half = (int)(3 + t * (11 - layer * 2));
                            c.FillRect(13 - half, y, half * 2, 1, leaf);
                        }
                    }

                    return c;
                }

                case PropKind.Cactus:
                {
                    var c = new Canvas(24, 46);
                    Color green = variant % 2 == 0 ? new Color(64, 128, 72) : new Color(52, 108, 62);
                    c.FillRect(10, 8, 5, 38, green);
                    c.FillRect(3, 18, 3, 14, green);
                    c.FillRect(3, 30, 8, 3, green);
                    c.FillRect(19, 22, 3, 12, green);
                    c.FillRect(14, 32, 8, 3, green);
                    return c;
                }

                case PropKind.Rock:
                {
                    var c = new Canvas(30, 20);
                    Color rock = variant % 2 == 0 ? new Color(104, 96, 88) : new Color(88, 82, 76);
                    c.FillEllipse(15, 16, 14, 6, rock);
                    c.FillEllipse(11, 12, 8, 6, rock);
                    c.FillEllipse(20, 13, 6, 5, rock);
                    c.FillRect(3, 16, 24, 4, new Color(70, 66, 62));
                    return c;
                }

                case PropKind.Lamp:
                {
                    var c = new Canvas(16, 64);
                    c.FillRect(7, 8, 2, 56, new Color(66, 70, 84));
                    c.FillRect(8, 6, 7, 3, new Color(66, 70, 84));
                    c.FillRect(12, 8, 4, 3, new Color(240, 226, 160));
                    c.FillEllipse(14, 14, 4, 3, new Color(255, 240, 170, 70));
                    return c;
                }

                case PropKind.Sign:
                {
                    var c = new Canvas(30, 34);
                    c.FillRect(13, 20, 4, 14, new Color(80, 82, 90));
                    Color face = variant == 0 ? new Color(30, 110, 200) : variant == 1 ? new Color(210, 180, 60) : new Color(60, 150, 90);
                    c.FillRect(2, 2, 26, 18, face);
                    c.Rect(2, 2, 26, 18, new Color(240, 240, 240));
                    for (int i = 0; i < 3; i++)
                    {
                        c.FillRect(6 + i * 6, 9, 4, 4, new Color(240, 240, 240));
                    }

                    return c;
                }

                case PropKind.SnowPole:
                {
                    var c = new Canvas(8, 46);
                    for (int i = 0; i < 6; i++)
                    {
                        c.FillRect(2, 2 + i * 7, 3, 4, i % 2 == 0 ? new Color(230, 60, 60) : new Color(240, 240, 240));
                    }

                    return c;
                }

                case PropKind.Building:
                {
                    var c = new Canvas(72, 128);
                    Color wall = variant % 2 == 0 ? new Color(52, 62, 92) : new Color(68, 56, 74);
                    c.FillRect(4, 12, 64, 116, wall);
                    c.FillRect(0, 0, 72, 13, new Color(
                        Math.Max(0, wall.R - 14),
                        Math.Max(0, wall.G - 14),
                        Math.Max(0, wall.B - 14)));
                    c.FillRect(4, 12, 64, 3, new Color(200, 200, 210));

                    var random = new Random(400 + variant * 31);
                    for (int y = 20; y < 118; y += 12)
                    {
                        for (int x = 10; x < 62; x += 12)
                        {
                            bool lit = random.Next(100) < 46;
                            c.FillRect(x, y, 7, 6, lit ? new Color(250, 226, 150) : new Color(26, 32, 48));
                        }
                    }

                    return c;
                }

                case PropKind.Billboard:
                {
                    var c = new Canvas(76, 52);
                    c.FillRect(34, 34, 6, 18, new Color(70, 72, 82));
                    Color frame = new Color(230, 230, 236);
                    c.FillRect(2, 2, 72, 34, frame);
                    Color ad = variant % 3 == 0
                        ? new Color(40, 100, 200)
                        : variant % 3 == 1 ? new Color(210, 70, 80) : new Color(240, 190, 60);
                    c.FillRect(5, 5, 66, 28, ad);
                    c.FillEllipse(24, 19, 12, 9, new Color(250, 250, 250));
                    c.FillRect(42, 10, 20, 4, new Color(250, 250, 250));
                    c.FillRect(42, 18, 16, 4, new Color(250, 250, 250));
                    c.FillRect(42, 26, 20, 3, new Color(250, 250, 250));
                    return c;
                }
            }

            var fallback = new Canvas(8, 8);
            fallback.Clear(new Color(120, 120, 140));
            return fallback;
        }

        public void Dispose()
        {
            foreach (Texture2D texture in _cache.Values)
            {
                texture?.Dispose();
            }

            _cache.Clear();

            foreach (Texture2D texture in _skyBack.Values)
            {
                texture?.Dispose();
            }

            foreach (Texture2D texture in _skyFront.Values)
            {
                texture?.Dispose();
            }

            foreach (Texture2D texture in _sun.Values)
            {
                texture?.Dispose();
            }

            PlayerCar?.Dispose();
            PlayerCarLeft?.Dispose();
            PlayerCarRight?.Dispose();
            Shadow?.Dispose();
            White?.Dispose();
            Badge?.Dispose();
            SplashArt?.Dispose();

            if (SuspectCars != null)
            {
                foreach (Texture2D texture in SuspectCars)
                {
                    texture?.Dispose();
                }
            }

            if (TrafficCars != null)
            {
                foreach (Texture2D texture in TrafficCars)
                {
                    texture?.Dispose();
                }
            }

            if (OncomingCars != null)
            {
                foreach (Texture2D texture in OncomingCars)
                {
                    texture?.Dispose();
                }
            }
        }
    }
}
