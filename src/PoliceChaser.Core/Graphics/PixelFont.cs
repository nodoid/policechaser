using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PoliceChaser.Graphics
{
    /// <summary>
    /// A 5x7 pixel font baked into one atlas at startup. The atlas is rendered at the game's
    /// high resolution scale, so text stays crisp instead of being upscaled from 320x240.
    /// </summary>
    public sealed class PixelFont
    {
        public const int GlyphWidth = 5;
        public const int GlyphHeight = 7;
        public const int Advance = 6;
        private const int Columns = 16;

        private static int Scale => RenderSettings.Scale;
        private static int Cell => 8 * Scale;

        private static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
            ['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
            ['D'] = new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C },
            ['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F },
            ['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 },
            ['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F },
            ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C },
            ['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
            ['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
            ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 },
            ['N'] = new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 },
            ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
            ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D },
            ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
            ['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
            ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
            ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 },
            ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
            ['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
            ['Z'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F },
            ['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
            ['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
            ['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E },
            ['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
            ['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
            ['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E },
            ['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
            ['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
            ['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C },
            [' '] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            ['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C },
            [','] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x0C, 0x04, 0x08 },
            [':'] = new byte[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 },
            ['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 },
            ['!'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04 },
            ['?'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x00, 0x04 },
            ['/'] = new byte[] { 0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10 },
            ['\''] = new byte[] { 0x0C, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00 },
            ['('] = new byte[] { 0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02 },
            [')'] = new byte[] { 0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08 },
            ['+'] = new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 },
            ['='] = new byte[] { 0x00, 0x00, 0x1F, 0x00, 0x1F, 0x00, 0x00 },
            ['*'] = new byte[] { 0x00, 0x15, 0x0E, 0x1F, 0x0E, 0x15, 0x00 },
            ['<'] = new byte[] { 0x02, 0x04, 0x08, 0x10, 0x08, 0x04, 0x02 },
            ['>'] = new byte[] { 0x08, 0x04, 0x02, 0x01, 0x02, 0x04, 0x08 },
            ['"'] = new byte[] { 0x0A, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00 },
            ['#'] = new byte[] { 0x0A, 0x0A, 0x1F, 0x0A, 0x1F, 0x0A, 0x0A },
            ['%'] = new byte[] { 0x11, 0x01, 0x02, 0x04, 0x08, 0x10, 0x11 },
            ['|'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 }
        };

        private readonly Dictionary<char, int> _index = new Dictionary<char, int>();
        private readonly List<char> _order = new List<char>();

        public Texture2D Atlas { get; }

        public PixelFont(GraphicsDevice device)
        {
            foreach (KeyValuePair<char, byte[]> pair in Glyphs)
            {
                _index[pair.Key] = _order.Count;
                _order.Add(pair.Key);
            }

            int cell = Cell;
            int rows = (_order.Count + Columns - 1) / Columns;
            var data = new Color[Columns * cell * rows * cell];
            var glyphColor = new Color(255, 255, 255, 255);

            for (int g = 0; g < _order.Count; g++)
            {
                byte[] glyph = Glyphs[_order[g]];
                int cx = (g % Columns) * cell;
                int cy = (g / Columns) * cell;

                for (int y = 0; y < GlyphHeight; y++)
                {
                    int bits = glyph[y];
                    for (int x = 0; x < GlyphWidth; x++)
                    {
                        if ((bits & (1 << (GlyphWidth - 1 - x))) == 0)
                        {
                            continue;
                        }

                        for (int dy = 0; dy < Scale; dy++)
                        {
                            for (int dx = 0; dx < Scale; dx++)
                            {
                                int px = cx + x * Scale + dx;
                                int py = cy + y * Scale + dy;
                                data[py * Columns * cell + px] = glyphColor;
                            }
                        }
                    }
                }
            }

            Atlas = new Texture2D(device, Columns * cell, rows * cell);
            Atlas.SetData(data);
        }

        public int Measure(string text, int scale = 1)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return text.Length * Advance * scale - scale;
        }

        /// <summary>Draws text at a logical (320x240 layout) position with integer scaling.</summary>
        public void Draw(SpriteBatch batch, string text, int x, int y, Color color, int scale = 1)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0)
            {
                return;
            }

            int cell = Cell;
            int unit = Scale * scale;
            int cursor = x * Scale;
            int lineY = y * Scale;

            for (int i = 0; i < text.Length; i++)
            {
                char c = char.ToUpperInvariant(text[i]);
                if (c == '\n')
                {
                    cursor = x * Scale;
                    lineY += (GlyphHeight + 3) * scale * Scale;
                    continue;
                }

                if (_index.TryGetValue(c, out int index))
                {
                    int sx = (index % Columns) * cell;
                    int sy = (index / Columns) * cell;
                    batch.Draw(
                        Atlas,
                        new Rectangle(cursor, lineY, GlyphWidth * unit, GlyphHeight * unit),
                        new Rectangle(sx, sy, GlyphWidth * Scale, GlyphHeight * Scale),
                        color);
                }

                cursor += Advance * scale * Scale;
            }
        }

        public void DrawShadowed(SpriteBatch batch, string text, int x, int y, Color color, Color shadow, int scale = 1)
        {
            Draw(batch, text, x + scale, y + scale, shadow, scale);
            Draw(batch, text, x, y, color, scale);
        }

        public void DrawCentered(SpriteBatch batch, string text, int centerX, int y, Color color, int scale = 1)
        {
            Draw(batch, text, centerX - Measure(text, scale) / 2, y, color, scale);
        }

        public void DrawCenteredShadowed(SpriteBatch batch, string text, int centerX, int y, Color color, Color shadow, int scale = 1)
        {
            DrawCentered(batch, text, centerX + scale, y + scale, shadow, scale);
            DrawCentered(batch, text, centerX, y, color, scale);
        }
    }
}
