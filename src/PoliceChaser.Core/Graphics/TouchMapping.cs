using System;
using Microsoft.Xna.Framework;

namespace PoliceChaser.Graphics
{
    /// <summary>
    /// Converts platform touch coordinates into the game's 320x240 logical layout.
    ///
    /// MonoGame reports touches in <c>TouchPanel.DisplayWidth/DisplayHeight</c> space, which on
    /// iOS is the preferred back buffer (1280x960) rather than the device resolution the game is
    /// actually drawn at. The platform scale therefore has to be undone first, and then the
    /// letterboxed 4:3 picture is mapped back to layout units.
    /// </summary>
    public static class TouchMapping
    {
        /// <summary>Turns a touch in display space back into window/back buffer pixels.</summary>
        public static Vector2 UndoTouchScale(Vector2 touch, float displayWidth, float displayHeight,
            float windowWidth, float windowHeight)
        {
            if (displayWidth <= 0f || displayHeight <= 0f || windowWidth <= 0f || windowHeight <= 0f)
            {
                return touch;
            }

            return new Vector2(
                touch.X * windowWidth / displayWidth,
                touch.Y * windowHeight / displayHeight);
        }

        /// <summary>
        /// Maps device pixels to logical layout units when the canvas fills the screen, as it does
        /// on phones: the 320x240 picture sits centred in a wider canvas with extra world either
        /// side, so only the margin has to be undone.
        /// </summary>
        public static Vector2 FullBleedToLogical(Vector2 pixels, float spaceWidth, float spaceHeight,
            float canvasWidth, float canvasHeight, float originX)
        {
            if (spaceWidth <= 0f || spaceHeight <= 0f || canvasWidth <= 0f || canvasHeight <= 0f)
            {
                return Vector2.Zero;
            }

            float canvasX = pixels.X * canvasWidth / spaceWidth - originX;
            float canvasY = pixels.Y * canvasHeight / spaceHeight;

            return new Vector2(
                canvasX / RenderSettings.Scale,
                canvasY / RenderSettings.Scale);
        }

        /// <summary>Maps picture pixels (with letterbox bars) to logical 320x240 coordinates.</summary>
        public static Vector2 ToLogical(Vector2 pixels, float spaceWidth, float spaceHeight)
        {
            if (spaceWidth <= 0f || spaceHeight <= 0f)
            {
                return Vector2.Zero;
            }

            float scale = MathF.Min(
                spaceWidth / RenderSettings.Width,
                spaceHeight / RenderSettings.Height);

            if (scale <= 0f)
            {
                return Vector2.Zero;
            }

            float width = RenderSettings.Width * scale;
            float height = RenderSettings.Height * scale;
            float offsetX = (spaceWidth - width) / 2f;
            float offsetY = (spaceHeight - height) / 2f;

            return new Vector2(
                (pixels.X - offsetX) / (width / RenderSettings.LogicalWidth),
                (pixels.Y - offsetY) / (height / RenderSettings.LogicalHeight));
        }
    }
}
