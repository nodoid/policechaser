using System;
using Foundation;
using UIKit;

namespace PoliceChaser.iOS
{
    /// <summary>
    /// App delegate. Since iOS 27 the UIKit scene lifecycle is mandatory, so the game is started
    /// from <see cref="SceneDelegate"/> and MonoGame's UIWindow is attached to that scene.
    /// </summary>
    [Register("AppDelegate")]
    internal class Program : UIApplicationDelegate
    {
        private static ChaseGame _game;

        public static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(Program));
        }

        internal static UIWindow MainWindow { get; private set; }

        internal static void StartInScene(UIWindowScene scene)
        {
            if (_game != null)
            {
                return;
            }

            _game = new ChaseGame();

            // Diagnostics hook: set PC_SELFTEST=wreck (or race, gameover, ...) in the launch
            // environment to drive one screen automatically. Inert in normal play.
            string selfTest = Environment.GetEnvironmentVariable("PC_SELFTEST");
            if (!string.IsNullOrEmpty(selfTest))
            {
                _game.EnableSelfTest(selfTest, 150, "selftest.png");
            }

            // MonoGame's iOS platform registers the UIKit window it created, which lets us
            // move it onto the scene instead of letting it float unattached.
            MainWindow = _game.Services.GetService(typeof(UIWindow)) as UIWindow;
            if (MainWindow != null)
            {
                MainWindow.WindowScene = scene;
                MainWindow.Frame = scene.CoordinateSpace.Bounds;
            }

            _game.Run();
            MainWindow?.MakeKeyAndVisible();
        }

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            return true;
        }

        public override void DidEnterBackground(UIApplication application)
        {
            _game?.PauseAudio();
        }

        public override void WillEnterForeground(UIApplication application)
        {
            _game?.ResumeAudio();
        }
    }

    [Register("SceneDelegate")]
    public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
    {
        [Export("window")]
        public UIWindow Window { get; set; }

        [Export("scene:willConnectToSession:options:")]
        public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            if (scene is UIWindowScene windowScene)
            {
                Program.StartInScene(windowScene);
                Window = Program.MainWindow;
            }
        }
    }
}
