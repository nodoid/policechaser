using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PoliceChaser.Desktop
{
    public static class Program
    {
        /// <summary>
        /// Entry point. Optional switches are used by the build's smoke tests:
        ///   --selftest &lt;screen&gt; [--frames N] [--shot path]
        ///   --selftest &lt;screen&gt; --video &lt;dir&gt; [--video-warmup N] [--video-frames N] [--fps N]
        /// The game renders normally and writes one PNG frame, then exits; the video form writes
        /// a fixed-timestep PNG sequence instead.
        /// </summary>
        [STAThread]
        public static int Main(string[] args)
        {
            string logRoot = AppContext.BaseDirectory;
            try
            {
                File.AppendAllText(Path.Combine(logRoot, "pc-startup.log"),
                    DateTime.UtcNow.ToString("O") + " start " + string.Join(" ", args) + Environment.NewLine);
            }
            catch (Exception)
            {
                // Logging must never stop the game from starting.
            }

            string screen = null;
            int frames = 90;
            string shot = null;
            string video = null;
            int videoWarmup = 0;
            int videoFrames = 0;
            int fps = 30;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--selftest":
                        if (i + 1 < args.Length)
                        {
                            screen = args[++i];
                        }

                        break;
                    case "--frames":
                        if (i + 1 < args.Length &&
                            int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                        {
                            frames = parsed;
                            i++;
                        }

                        break;
                    case "--shot":
                        if (i + 1 < args.Length)
                        {
                            shot = args[++i];
                        }

                        break;
                    case "--video":
                        if (i + 1 < args.Length)
                        {
                            video = args[++i];
                        }

                        break;
                    case "--video-warmup":
                        if (i + 1 < args.Length &&
                            int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int warmup))
                        {
                            videoWarmup = warmup;
                            i++;
                        }

                        break;
                    case "--video-frames":
                        if (i + 1 < args.Length &&
                            int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int captureFrames))
                        {
                            videoFrames = captureFrames;
                            i++;
                        }

                        break;
                    case "--fps":
                        if (i + 1 < args.Length &&
                            int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rate))
                        {
                            fps = rate;
                            i++;
                        }

                        break;
                }
            }

            try
            {
                using var game = new ChaseGame();

                if (screen != null && video != null)
                {
                    game.EnableSelfTestVideo(screen, videoWarmup, videoFrames, fps, video);
                }
                else if (screen != null)
                {
                    game.EnableSelfTest(screen, frames, shot ?? "selftest.png");
                }

                game.Run();
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(logRoot, "pc-crash.log"), ex.ToString());
                }
                catch (Exception)
                {
                }

                return 1;
            }
        }
    }
}
