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
    /// Scanline pseudo-3D road renderer in the style of the 8-bit chase games: the road is drawn
    /// as horizontal bands with per-scanline perspective, sprites are projected by depth and
    /// clipped against hills.
    /// </summary>
    public sealed class RoadRenderer
    {
        public const int ScreenWidth = 320;
        public const int ScreenHeight = 240;
        public const int ViewTop = 18;
        public const int ViewBottom = 208;
        public const int Horizon = 92;
        public const int DrawDistance = 150;

        private const float CameraDepth = 0.84f;
        private const float RoadWidth = 2000f;
        private const float CameraHeight = 1000f;
        private const float HalfScreen = ScreenWidth / 2f;
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

        /// <summary>Horizontal skyline drift, accumulated from curves by the game.</summary>
        public float SkylineOffset { get; set; }

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

                Projected near = Project(pixel, -x, y0, z0, cameraZ, camX, cameraY, shakeX, shakeY);
                Projected far = Project(pixel, -x - dx, y1, z1, cameraZ, camX, cameraY, shakeX, shakeY);

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

                DrawBand(batch, pixel, palette, idx, n, near, far, fog, maxy);

                _clip[idx] = (int)Math.Clamp(near.Y, ViewTop, ViewBottom);
                _near[_drawn] = near;
                _far[_drawn] = far;
                _index[_drawn] = idx;
                _drawn++;

                maxy = far.Y;
            }

            DrawSprites(batch, art, road, palette, sprites, cameraZ, camX, cameraY, shakeX, shakeY);
        }

        private void DrawSky(SpriteBatch batch, Texture2D pixel, Art art, ThemePalette palette, MissionTheme theme)
        {
            for (int y = 0; y < Horizon; y++)
            {
                float t = y / (float)Horizon;
                Color color = Color.Lerp(palette.SkyTop, palette.SkyBottom, t * t);
                batch.Draw(pixel, new Rectangle(0, y, ScreenWidth, 1), color);
            }

            Texture2D sun = art.Sun(theme);
            int sunX = (int)(160 - SkylineOffset * 0.18f) - sun.Width / 2;
            sunX = ((sunX % 900) + 900) % 900 - 120;
            batch.Draw(sun, new Rectangle(sunX, 26, sun.Width, sun.Height), Color.White);

            DrawSkylineLayer(batch, art.Skyline(theme, false), Horizon + 4, SkylineOffset * 0.22f, 1f);
            DrawSkylineLayer(batch, art.Skyline(theme, true), Horizon + 6, SkylineOffset * 0.5f, 1f);

            // Haze that hides the seam between skyline and road.
            for (int y = Horizon - 6; y < Horizon + 10; y++)
            {
                float t = (y - (Horizon - 6)) / 16f;
                int alpha = (int)(70 * (1f - MathF.Abs(t - 0.5f) * 2f));
                if (alpha > 0)
                {
                    batch.Draw(pixel, new Rectangle(0, y, ScreenWidth, 1), new Color(palette.Fog.R, palette.Fog.G, palette.Fog.B, alpha));
                }
            }
        }

        private static void DrawSkylineLayer(SpriteBatch batch, Texture2D texture, int bottomY, float offset, float alpha)
        {
            int width = texture.Width;
            int start = -(int)(((offset % width) + width) % width);
            int y = bottomY - texture.Height;

            for (int x = start; x < ScreenWidth; x += width)
            {
                batch.Draw(texture, new Rectangle(x, y, width, texture.Height), Color.White * alpha);
            }
        }

        private void DrawBand(
            SpriteBatch batch,
            Texture2D pixel,
            ThemePalette palette,
            int segmentIndex,
            int distance,
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

                batch.Draw(pixel, new Rectangle(0, y, ScreenWidth, 1), grass);
                batch.Draw(pixel, new Rectangle(left, y, width, 1), road);

                int rumbleWidth = Math.Max(1, (int)(half * 0.11f));
                batch.Draw(pixel, new Rectangle(left, y, rumbleWidth, 1), rumble);
                batch.Draw(pixel, new Rectangle(right - rumbleWidth, y, rumbleWidth, 1), rumble);

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
                    batch.Draw(pixel, new Rectangle(laneLeft, y, laneWidth, 1), lane);
                    batch.Draw(pixel, new Rectangle(laneRight, y, laneWidth, 1), lane);
                }
            }
        }

        private void DrawSprites(
            SpriteBatch batch,
            Art art,
            Road road,
            ThemePalette palette,
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
                float sx = HalfScreen + scale * ((sprite.X * RoadWidth) - camX) * HalfScreen + shakeX;
                float groundY = Horizon - scale * ((road.HeightAt(sprite.Z) - cameraY) * HalfHeight) + shakeY;
                float w = sprite.WorldWidth * scale * HalfScreen;
                if (w < 1f)
                {
                    continue;
                }

                // Anything that would balloon past a quarter of the screen is a roadside
                // object that has slid too close to the camera; drop it rather than let it
                // cover the road.
                if (w > 190f)
                {
                    continue;
                }

                float h = w * sprite.Texture.Height / sprite.Texture.Width;
                int drawX = (int)(sx - w / 2f);
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

        private Projected Project(Texture2D pixel, float offsetX, float worldY, float worldZ, float cameraZ, float camX, float cameraY, float shakeX, float shakeY)
        {
            float dz = worldZ - cameraZ;
            if (dz < 0.01f)
            {
                dz = 0.01f;
            }

            float scale = CameraDepth / dz;
            return new Projected
            {
                X = HalfScreen + scale * (offsetX - camX) * HalfScreen + shakeX,
                Y = Horizon - scale * (worldY - cameraY) * HalfHeight + shakeY,
                W = scale * RoadWidth * HalfScreen
            };
        }

        /// <summary>Projects an arbitrary point; used for the suspect and traffic markers.</summary>
        public static Vector2 ProjectPoint(float x, float z, float cameraZ, float playerX, float cameraY, float groundY)
        {
            float dz = MathF.Max(0.01f, z - cameraZ);
            float scale = CameraDepth / dz;
            return new Vector2(
                HalfScreen + scale * ((x * RoadWidth) - playerX * RoadWidth) * HalfScreen,
                Horizon - scale * (groundY - cameraY) * HalfHeight);
        }
    }
}
