using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoliceChaser.Game;

namespace PoliceChaser.Graphics
{
    public struct WorldSprite
    {
        public float Z;
        public float X;
        public Texture2D Texture;
        public float WorldWidth;
        public Color Tint;
    }

    /// <summary>
    /// Scanline pseudo-3D road renderer in the style of the 8-bit chase games. It renders
    /// directly in target pixels at <see cref="RenderSettings.Scale"/> times the logical
    /// resolution, so the road bands, skyline and sprites are genuinely high resolution.
    /// </summary>
    public sealed class RoadRenderer
    {
        private const int S = RenderSettings.Scale;

        public const int ScreenHeight = RenderSettings.Height;
        public const int ViewTop = 18 * S;
        public const int ViewBottom = 208 * S;
        public const int Horizon = 92 * S;
        public const int DrawDistance = 150;

        private const float CameraDepth = 0.84f;
        private const float RoadWidth = 2000f;
        private const float CameraHeight = 1000f;

        // The projection keeps the field of view of the authored 4:3 picture, so a wider canvas
        // reveals more of the world instead of stretching what was already on screen.
        private const float FocalX = RenderSettings.Width / 2f;
        private const float HalfHeight = ScreenHeight / 2f;

        private struct Projected
        {
            public float X;
            public float Y;
            public float W;
        }

        private readonly int[] _clip = new int[Road.SegmentCount + 512];
        private readonly Projected[] _near = new Projected[DrawDistance + 2];
        private readonly Projected[] _far = new Projected[DrawDistance + 2];
        private readonly int[] _index = new int[DrawDistance + 2];
        private int _drawn;

        /// <summary>Horizontal skyline drift, accumulated from curves by the game (logical units).</summary>
        public float SkylineOffset { get; set; }

        /// <summary>Width of the canvas being drawn into; phones widen it to fill the screen.</summary>
        public int ScreenWidth { get; set; } = RenderSettings.Width;

        /// <summary>Left edge of the centred 4:3 picture inside that canvas, in pixels.</summary>
        public int OriginX { get; set; }

        private float CentreX => ScreenWidth / 2f;

        public void Render(
            SpriteBatch batch,
            Texture2D pixel,
            Art art,
            Road road,
            ThemePalette palette,
            MissionTheme theme,
            float cameraZ,
            float cameraX,
            float cameraY,
            float shakeX,
            float shakeY,
            IReadOnlyList<WorldSprite> sprites)
        {
            DrawSky(batch, pixel, art, palette, theme);

            float camX = cameraX * RoadWidth;
            int baseIndex = road.IndexAt(cameraZ);
            float basePercent = (cameraZ % Road.SegmentLength) / Road.SegmentLength;

            for (int i = 0; i < _clip.Length; i++)
            {
                _clip[i] = ViewBottom;
            }

            float x = 0f;
            float dx = -(road.Segments[baseIndex].Curve * basePercent);
            float maxy = ViewBottom;
            _drawn = 0;

            for (int n = 0; n < DrawDistance; n++)
            {
                int idx = (baseIndex + n) % road.Count;
                int nextIdx = (idx + 1) % road.Count;
                RoadSegment segment = road.Segments[idx];

                float z0 = idx * Road.SegmentLength;
                float z1 = (idx + 1) * Road.SegmentLength;
                float y0 = segment.Y;
                float y1 = road.Segments[nextIdx].Y;

                Projected near = Project(-x, y0, z0, cameraZ, camX, cameraY, shakeX, shakeY);
                Projected far = Project(-x - dx, y1, z1, cameraZ, camX, cameraY, shakeX, shakeY);

                x += dx;
                dx += segment.Curve;

                if (z0 - cameraZ <= CameraDepth)
                {
                    continue;
                }

                if (far.Y >= near.Y || far.Y >= maxy)
                {
                    continue;
                }

                float fog = n / (float)DrawDistance;
                fog = MathF.Min(1f, fog * fog * 1.15f);

                DrawBand(batch, pixel, palette, idx, near, far, fog, maxy);

                _clip[idx] = (int)Math.Clamp(near.Y, ViewTop, ViewBottom);
                _near[_drawn] = near;
                _far[_drawn] = far;
                _index[_drawn] = idx;
                _drawn++;

                maxy = far.Y;
            }

            DrawSprites(batch, art, road, sprites, cameraZ, camX, cameraY, shakeX, shakeY);
        }

        private void DrawSky(SpriteBatch batch, Texture2D pixel, Art art, ThemePalette palette, MissionTheme theme)
        {
            for (int y = 0; y < Horizon; y++)
            {
                float t = y / (float)Horizon;
                Color color = Color.Lerp(palette.SkyTop, palette.SkyBottom, t * t);
                Fill(batch, pixel, 0, y, ScreenWidth, 1, color);
            }

            float drift = SkylineOffset * S;

            Texture2D sun = art.Sun(theme);
            int sunX = (int)(CentreX - drift * 0.18f) - sun.Width / 2;
            sunX = ((sunX % (900 * S)) + 900 * S) % (900 * S) - 120 * S;
            Blit(batch, sun, sunX, 26 * S, sun.Width, sun.Height, Color.White);

            DrawSkylineLayer(batch, art.Skyline(theme, false), Horizon + 4 * S, drift * 0.22f, 1f);
            DrawSkylineLayer(batch, art.Skyline(theme, true), Horizon + 6 * S, drift * 0.5f, 1f);

            for (int y = Horizon - 6 * S; y < Horizon + 10 * S; y++)
            {
                float t = (y - (Horizon - 6 * S)) / (16f * S);
                int alpha = (int)(70 * (1f - MathF.Abs(t - 0.5f) * 2f));
                if (alpha > 0)
                {
                    Fill(batch, pixel, 0, y, ScreenWidth, 1,
                        new Color(palette.Fog.R, palette.Fog.G, palette.Fog.B, alpha));
                }
            }
        }

        private void DrawSkylineLayer(SpriteBatch batch, Texture2D texture, int bottomY, float offset, float alpha)
        {
            int width = texture.Width;
            int start = -(int)(((offset % width) + width) % width);
            int y = bottomY - texture.Height;

            for (int x = start; x < ScreenWidth; x += width)
            {
                Blit(batch, texture, x, y, width, texture.Height, Color.White * alpha);
            }
        }

        /// <summary>Fills a rectangle given in canvas pixels, accounting for the picture's margin.</summary>
        private void Fill(SpriteBatch batch, Texture2D pixel, float x, float y, float w, float h, Color color)
        {
            batch.Draw(pixel, new Rectangle((int)x - OriginX, (int)y, (int)w, (int)h), color);
        }

        /// <summary>Draws a texture given in canvas pixels, accounting for the picture's margin.</summary>
        private void Blit(SpriteBatch batch, Texture2D texture, int x, int y, int w, int h, Color tint)
        {
            batch.Draw(texture, new Rectangle(x - OriginX, y, w, h), tint);
        }

        private void DrawBand(
            SpriteBatch batch,
            Texture2D pixel,
            ThemePalette palette,
            int segmentIndex,
            Projected near,
            Projected far,
            float fog,
            float maxy)
        {
            int yFar = (int)MathF.Max(far.Y, ViewTop);
            int yNear = (int)MathF.Min(near.Y, MathF.Min(ViewBottom, maxy));
            if (yNear <= yFar)
            {
                return;
            }

            bool alt = (segmentIndex / 3) % 2 == 0;
            Color grass = palette.Grass;
            Color road = alt ? palette.Road : palette.RoadDark;
            Color rumble = alt ? palette.RumbleLight : palette.RumbleDark;

            if (fog > 0.01f)
            {
                grass = Color.Lerp(grass, palette.Fog, fog);
                road = Color.Lerp(road, palette.Fog, fog);
                rumble = Color.Lerp(rumble, palette.Fog, fog);
            }

            float span = near.Y - far.Y;
            if (span < 0.0001f)
            {
                span = 0.0001f;
            }

            for (int y = yFar; y < yNear; y++)
            {
                float t = (y - far.Y) / span;
                float cx = far.X + (near.X - far.X) * t;
                float half = far.W + (near.W - far.W) * t;

                int left = (int)(cx - half);
                int right = (int)(cx + half);
                int width = right - left;
                if (width <= 0)
                {
                    continue;
                }

                Fill(batch, pixel, 0, y, ScreenWidth, 1, grass);
                Fill(batch, pixel, left, y, width, 1, road);

                int rumbleWidth = Math.Max(1, (int)(half * 0.11f));
                Fill(batch, pixel, left, y, rumbleWidth, 1, rumble);
                Fill(batch, pixel, right - rumbleWidth, y, rumbleWidth, 1, rumble);

                if ((segmentIndex % 6) < 3)
                {
                    int laneWidth = Math.Max(1, (int)(half * 0.025f));
                    Color lane = palette.Lane;
                    if (fog > 0.01f)
                    {
                        lane = Color.Lerp(lane, palette.Fog, fog);
                    }

                    int laneLeft = (int)(cx - half * 0.34f);
                    int laneRight = (int)(cx + half * 0.34f);
                    Fill(batch, pixel, laneLeft, y, laneWidth, 1, lane);
                    Fill(batch, pixel, laneRight, y, laneWidth, 1, lane);
                }
            }
        }

        private void DrawSprites(
            SpriteBatch batch,
            Art art,
            Road road,
            IReadOnlyList<WorldSprite> sprites,
            float cameraZ,
            float camX,
            float cameraY,
            float shakeX,
            float shakeY)
        {
            if (sprites == null || sprites.Count == 0)
            {
                return;
            }

            var sorted = new List<WorldSprite>(sprites);
            sorted.Sort((a, b) => b.Z.CompareTo(a.Z));

            for (int i = 0; i < sorted.Count; i++)
            {
                WorldSprite sprite = sorted[i];
                float dz = sprite.Z - cameraZ;
                if (dz < 700f || dz > DrawDistance * Road.SegmentLength)
                {
                    continue;
                }

                if (sprite.Texture == null)
                {
                    continue;
                }

                float scale = CameraDepth / dz;
                float sx = CentreX + scale * ((sprite.X * RoadWidth) - camX) * FocalX + shakeX;
                float groundY = Horizon - scale * ((road.HeightAt(sprite.Z) - cameraY) * HalfHeight) + shakeY;
                float w = sprite.WorldWidth * scale * FocalX;
                if (w < 1f)
                {
                    continue;
                }

                // Roadside objects that slide too close to the camera are culled rather than
                // allowed to cover the carriageway.
                if (w > 190f * S)
                {
                    continue;
                }

                float h = w * sprite.Texture.Height / sprite.Texture.Width;
                int drawX = (int)(sx - w / 2f) - OriginX;
                int drawY = (int)(groundY - h);

                int segIdx = road.IndexAt(sprite.Z);
                int clipY = Math.Min(ViewBottom, _clip[segIdx]);
                if (drawY > clipY)
                {
                    continue;
                }

                var destination = new Rectangle(drawX, drawY, Math.Max(1, (int)w), Math.Max(1, (int)h));
                batch.Draw(sprite.Texture, destination, sprite.Tint);
            }
        }

        private Projected Project(float offsetX, float worldY, float worldZ, float cameraZ, float camX, float cameraY, float shakeX, float shakeY)
        {
            float dz = worldZ - cameraZ;
            if (dz < 0.01f)
            {
                dz = 0.01f;
            }

            float scale = CameraDepth / dz;
            return new Projected
            {
                X = CentreX + scale * (offsetX - camX) * FocalX + shakeX,
                Y = Horizon - scale * (worldY - cameraY) * HalfHeight + shakeY,
                W = scale * RoadWidth * FocalX
            };
        }

        /// <summary>Projects an arbitrary point (target pixels); used for markers.</summary>
        public static Vector2 ProjectPoint(float x, float z, float cameraZ, float playerX, float cameraY, float groundY)
        {
            float dz = MathF.Max(0.01f, z - cameraZ);
            float scale = CameraDepth / dz;
            return new Vector2(
                RenderSettings.Width / 2f + scale * ((x * RoadWidth) - playerX * RoadWidth) * FocalX,
                Horizon - scale * (groundY - cameraY) * HalfHeight);
        }
    }
}
