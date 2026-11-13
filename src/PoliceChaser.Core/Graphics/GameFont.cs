using System;
using System.Collections.Generic;
using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PoliceChaser.Graphics
{
    /// <summary>
    /// Anti-aliased outline font rasterised at the game's high resolution scale. Sizes are given
    /// in logical (320x240 layout) pixels and baked per scale step, so text stays smooth instead
    /// of being upscaled from the low resolution canvas.
    /// </summary>
    public sealed class GameFont : IDisposable
    {
        /// <summary>Logical height of one text step; one step has the width of the old 6px cells.</summary>
        private const int BaseSize = 10;
        private const int LineStep = 10;
        private const float CapTrim = 0.25f;

        private const string ResourceName = "PoliceChaser.Graphics.Fonts.ChakraPetch-SemiBold.ttf";

        private readonly FontSystem _system;
        private readonly Dictionary<int, SpriteFontBase> _fonts = new Dictionary<int, SpriteFontBase>();

        public GameFont()
        {
            var settings = new FontSystemSettings
            {
                TextureWidth = 2048,
                TextureHeight = 2048
            };

            _system = new FontSystem(settings);

            using Stream resource = typeof(GameFont).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("Embedded font " + ResourceName + " is missing.");
            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            _system.AddFont(buffer.ToArray());
        }

        private SpriteFontBase GetFont(int scale)
        {
            if (!_fonts.TryGetValue(scale, out SpriteFontBase font))
            {
                font = _system.GetFont(BaseSize * RenderSettings.Scale * scale);
                _fonts[scale] = font;
            }

            return font;
        }

        private static float Unit()
        {
            return RenderSettings.Scale;
        }

        /// <summary>Width of the widest line, rounded to whole logical pixels.</summary>
        public int Measure(string text, int scale = 1)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0)
            {
                return 0;
            }

            SpriteFontBase font = GetFont(scale);
            float widest = 0f;
            int start = 0;

            while (start <= text.Length)
            {
                int end = text.IndexOf('\n', start);
                string line = end < 0 ? text.Substring(start) : text.Substring(start, end - start);
                widest = MathF.Max(widest, font.MeasureString(line).X);
                if (end < 0)
                {
                    break;
                }

                start = end + 1;
            }

            return (int)MathF.Round(widest / Unit());
        }

        /// <summary>Draws text at a logical (320x240 layout) position.</summary>
        public void Draw(SpriteBatch batch, string text, int x, int y, Color color, int scale = 1)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0)
            {
                return;
            }

            SpriteFontBase font = GetFont(scale);
            float unit = Unit();
            float size = BaseSize * RenderSettings.Scale * scale;
            float top = y * unit - size * CapTrim;
            int line = 0;
            int start = 0;

            while (start <= text.Length)
            {
                int end = text.IndexOf('\n', start);
                string part = end < 0 ? text.Substring(start) : text.Substring(start, end - start);
                batch.DrawString(font, part, new Vector2(x * unit, top + line * LineStep * scale * unit), color);

                if (end < 0)
                {
                    break;
                }

                start = end + 1;
                line++;
            }
        }

        public void DrawShadowed(SpriteBatch batch, string text, int x, int y, Color color, Color shadow, int scale = 1)
        {
            // A fixed one-pixel drop shadow reads as depth; scaling it up just looks like
            // a second, misprinted copy of the word behind the first.
            Draw(batch, text, x + 1, y + 1, shadow, scale);
            Draw(batch, text, x, y, color, scale);
        }

        public void DrawCentered(SpriteBatch batch, string text, int centerX, int y, Color color, int scale = 1)
        {
            Draw(batch, text, centerX - Measure(text, scale) / 2, y, color, scale);
        }

        public void DrawCenteredShadowed(SpriteBatch batch, string text, int centerX, int y, Color color, Color shadow, int scale = 1)
        {
            DrawCentered(batch, text, centerX + 1, y + 1, shadow, scale);
            DrawCentered(batch, text, centerX, y, color, scale);
        }

        public void Dispose()
        {
            _fonts.Clear();
            _system.Dispose();
        }
    }
}
