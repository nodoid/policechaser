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
        /// The game renders normally and writes one PNG frame, then exits.
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
                }
            }

            try
            {
                using var game = new ChaseGame();

                if (screen != null)
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
