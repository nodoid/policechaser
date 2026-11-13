namespace PoliceChaser.Graphics
{
    /// <summary>
    /// The game's layout is authored in a 320x240 grid, but everything is rendered at
    /// <see cref="Scale"/> times that size so shapes, text and the road are genuinely
    /// high resolution instead of an upscaled low-res framebuffer.
    /// </summary>
    public static class RenderSettings
    {
        public const int Scale = 4;

        public const int LogicalWidth = 320;
        public const int LogicalHeight = 240;

        public const int Width = LogicalWidth * Scale;
        public const int Height = LogicalHeight * Scale;
    }
}
