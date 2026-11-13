using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using PoliceChaser.Audio;
using PoliceChaser.Game;
using PoliceChaser.Graphics;

namespace PoliceChaser
{
    public enum GameScreen
    {
        Splash,
        Title,
        DifficultySelect,
        MissionSelect,
        Briefing,
        Racing,
        Busted,
        MissionCleared,
        CampaignComplete,
        GameOver,
        NameEntry,
        HighScores,
        Paused
    }

    /// <summary>
    /// The whole game. Art is baked at 4x and drawn to a 1280x960 render target that is smoothly
    /// scaled to fit any window or device, so the presentation is high resolution everywhere.
    /// </summary>
    public sealed class ChaseGame : Microsoft.Xna.Framework.Game
    {
        public const int VirtualWidth = RenderSettings.LogicalWidth;
        public const int VirtualHeight = RenderSettings.LogicalHeight;
        private const int S = RenderSettings.Scale;

#if ANDROID || IOS
        private const bool MobilePlatform = true;
#else
        private const bool MobilePlatform = false;
#endif

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private RenderTarget2D _target;
        private Texture2D _white;
        private GameFont _font;
        private Art _art;
        private ChipAudio _audio;
        private HighScoreStore _scores;
        private readonly RoadRenderer _roadRenderer = new RoadRenderer();
        private readonly List<WorldSprite> _worldSprites = new List<WorldSprite>();
        private readonly Random _random = new Random(20261113);

        private GameScreen _screen = GameScreen.Splash;
        private float _screenTime;
        private float _time;

        private Difficulty _difficulty = Difficulty.Beginner;
        private int _difficultyCursor;
        private int _missionCursor;
        private Mission _mission;
        private Race _race;
        private int _campaignIndex;
        private int _campaignRaw;
        private bool _scoreSubmitted;
        private int _finalScore;
        private ScoreEntry _pendingEntry;
        private int _highlightIndex = -1;
        private int _lastRank;
        private string _lastName = "PC1";
        private int _nameCursor;
        private char[] _nameLetters = { 'P', 'C', '1' };

        private float _countdown;
        private int _lastCountdownSecond = -1;
        private float _raceEndTimer;
        private string _radioText = "";
        private float _radioTimer;
        private float _radioFlash;
        private float _skyOffset;
        private float _shake;
        private float _lastSpeedRatio;
        private bool _paused;

        private InputFrame _input;
        private bool _PointerSeen;
        private bool _mouseWasDown;
        private TouchCollection _previousTouches;
        private bool _selfTest;
        private string _selfTestScreen = "title";
        private string _selfTestPath = "selftest.png";
        private int _selfTestFrames = 90;
        private int _selfTestFrame;
        private bool _selfTestTapped;

        private struct InputFrame
        {
            public float Steer;
            public bool Up;
            public bool Down;
            public bool Left;
            public bool Right;
            public bool Confirm;
            public bool Back;
            public bool Pause;
            public bool Turbo;
            public bool Brake;
            public Vector2 Pointer;
            public bool PointerDown;
            public bool PointerPressed;
        }

        public ChaseGame()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 960,
                SynchronizeWithVerticalRetrace = true,
                IsFullScreen = false,
                GraphicsProfile = GraphicsProfile.Reach
            };

            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
            Window.AllowUserResizing = true;
            Window.Title = "Police Chaser";
        }

        protected override void Initialize()
        {
            Window.Title = "Police Chaser";
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _target = new RenderTarget2D(GraphicsDevice, RenderSettings.Width, RenderSettings.Height, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            _white = new Texture2D(GraphicsDevice, 1, 1);
            _white.SetData(new[] { Color.White });

            _font = new GameFont();
            _art = new Art(GraphicsDevice);
            _audio = new ChipAudio();
            _scores = new HighScoreStore(new LocalHighScoreFile(LocalHighScoreFile.DefaultRoot));

            TouchPanel.EnableMouseTouchPoint = true;
            TouchPanel.EnableMouseGestures = false;

            if (_selfTest)
            {
                // Never let a smoke test touch (or pollute) the player's real table.
                _scores = new HighScoreStore(new MemoryHighScoreFile());
                SeedSelfTestScores();
                PrepareSelfTest();
            }


            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            _scores?.Save();
            _audio?.Dispose();
            _art?.Dispose();
            _font?.Dispose();
            _white?.Dispose();
            _target?.Dispose();
            base.UnloadContent();
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            _scores?.Save();
            base.OnExiting(sender, args);
        }

        /// <summary>Called by the mobile hosts when the app goes to the background.</summary>
        public void PauseAudio()
        {
            _audio?.PauseRaceAudio(true);
        }

        /// <summary>Called by the mobile hosts when the app comes back to the foreground.</summary>
        public void ResumeAudio()
        {
            _audio?.PauseRaceAudio(false);
        }

        /// <summary>
        /// Configuration hook for automated smoke tests: shows a specific screen, renders a
        /// number of frames, writes one PNG and exits. Used by the macOS and Windows build checks.
        /// </summary>
        public void EnableSelfTest(string screen, int frames, string shotPath)
        {
            _selfTest = true;
            _selfTestScreen = string.IsNullOrEmpty(screen) ? "title" : screen.ToLowerInvariant();
            _selfTestFrames = Math.Max(1, frames);
            _selfTestPath = string.IsNullOrEmpty(shotPath) ? "selftest.png" : shotPath;
        }

        // ===================================================================== update

        protected override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt > 0.1f)
            {
                dt = 0.1f;
            }

            _time += dt;
            _screenTime += dt;

            if (_selfTest)
            {
                UpdateSelfTest(dt);
                base.Update(gameTime);
                return;
            }

            ReadInput();
            UpdateRadio(dt);

            // A portrait window cannot show the 4:3 picture usefully; hold the game on a prompt
            // until the device or window is turned back to landscape.
            if (PortraitWindow)
            {
                base.Update(gameTime);
                return;
            }

            switch (_screen)
            {
                case GameScreen.Splash:
                    UpdateSplash();
                    break;
                case GameScreen.Title:
                    UpdateTitle();
                    break;
                case GameScreen.DifficultySelect:
                    UpdateDifficultySelect();
                    break;
                case GameScreen.MissionSelect:
                    UpdateMissionSelect();
                    break;
                case GameScreen.Briefing:
                    UpdateBriefing();
                    break;
                case GameScreen.Racing:
                    UpdateRacing(dt);
                    break;
                case GameScreen.Paused:
                    UpdatePaused();
                    break;
                case GameScreen.Busted:
                    UpdateBusted();
                    break;
                case GameScreen.MissionCleared:
                    UpdateMissionCleared();
                    break;
                case GameScreen.CampaignComplete:
                    UpdateCampaignComplete();
                    break;
                case GameScreen.GameOver:
                    UpdateGameOver();
                    break;
                case GameScreen.NameEntry:
                    UpdateNameEntry();
                    break;
                case GameScreen.HighScores:
                    UpdateHighScores();
                    break;
            }

            if (_audio != null)
            {
                _audio.UpdateRaceAudio(_lastSpeedRatio, _race?.OffRoad ?? false, _paused, _input.Turbo);
            }

            base.Update(gameTime);
        }

        private void SetScreen(GameScreen screen)
        {
            if (_screen == GameScreen.Racing && screen != GameScreen.Paused)
            {
                _audio.StopRaceAudio();
            }

            _screen = screen;
            _screenTime = 0;

            switch (screen)
            {
                case GameScreen.Splash:
                    _audio.StopMusic();
                    break;
                case GameScreen.Title:
                    _audio.PlayMusic(ChipAudio.TrackTitle);
                    break;
                case GameScreen.DifficultySelect:
                case GameScreen.MissionSelect:
                case GameScreen.Briefing:
                    _audio.PlayMusic(ChipAudio.TrackTitle);
                    break;
                case GameScreen.Racing:
                    _audio.PlayMusic(ChipAudio.TrackRace);
                    _audio.StartRaceEngine();
                    _paused = false;
                    break;
                case GameScreen.Paused:
                    _audio.PauseRaceAudio(true);
                    break;
                case GameScreen.Busted:
                    _audio.PlayMusic(ChipAudio.TrackBusted, false);
                    break;
                case GameScreen.MissionCleared:
                    _audio.PlayMusic(ChipAudio.TrackHighScore);
                    break;
                case GameScreen.CampaignComplete:
                case GameScreen.GameOver:
                    _audio.StopRaceAudio();
                    _audio.PlayMusic(ChipAudio.TrackGameOver, false);
                    break;
                case GameScreen.NameEntry:
                case GameScreen.HighScores:
                    _audio.PlayMusic(ChipAudio.TrackHighScore);
                    break;
            }
        }

        private void UpdateSplash()
        {
            if (_screenTime > 3.8f || (_screenTime > 0.8f && _input.Confirm))
            {
                SetScreen(GameScreen.Title);
            }
        }

        private void UpdateTitle()
        {
            if (_input.PointerPressed && new Rectangle(196, 182, 124, 34).Contains(_input.Pointer.ToPoint()))
            {
                _highlightIndex = -1;
                _audio.Play(_audio.Select);
                SetScreen(GameScreen.HighScores);
                return;
            }

            if (_input.Confirm)
            {
                _audio.Play(_audio.Select);
                _difficultyCursor = (int)_difficulty;
                SetScreen(GameScreen.DifficultySelect);
            }
        }

        private void UpdateDifficultySelect()
        {
            if (_input.Up)
            {
                _difficultyCursor = (_difficultyCursor + 2) % 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Down)
            {
                _difficultyCursor = (_difficultyCursor + 1) % 3;
                _audio.Play(_audio.Select);
            }

            int tapped = TappedRow(3, 82, 42);
            if (tapped >= 0)
            {
                _difficultyCursor = tapped;
                _input.Confirm = true;
            }

            if (_input.Confirm)
            {
                _difficulty = DifficultyRules.All[_difficultyCursor];
                _audio.Play(_audio.Beep);
                _missionCursor = 0;
                SetScreen(GameScreen.MissionSelect);
            }
            else if (_input.Back)
            {
                SetScreen(GameScreen.Title);
            }
        }

        private void UpdateMissionSelect()
        {
            if (_input.Left && _missionCursor % 3 > 0)
            {
                _missionCursor--;
                _audio.Play(_audio.Select);
            }

            if (_input.Right && _missionCursor % 3 < 2)
            {
                _missionCursor++;
                _audio.Play(_audio.Select);
            }

            if (_input.Up && _missionCursor >= 3)
            {
                _missionCursor -= 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Down && _missionCursor + 3 < Mission.All.Count)
            {
                _missionCursor += 3;
                _audio.Play(_audio.Select);
            }

            Rectangle[] cards = MissionCards();
            for (int i = 0; i < cards.Length; i++)
            {
                if (TapOn(cards[i]))
                {
                    _missionCursor = i;
                    _input.Confirm = true;
                }
            }

            if (_input.Confirm)
            {
                _mission = Mission.All[_missionCursor];
                _audio.Play(_audio.Beep);
                SetScreen(GameScreen.Briefing);
            }
            else if (_input.Back)
            {
                SetScreen(GameScreen.DifficultySelect);
            }
        }

        private void UpdateBriefing()
        {
            if (_input.Confirm || TapOn(new Rectangle(0, 0, VirtualWidth, VirtualHeight)))
            {
                StartCampaign();
            }
            else if (_input.Back)
            {
                SetScreen(GameScreen.MissionSelect);
            }
        }

        private void StartCampaign()
        {
            _campaignIndex = _missionCursor;
            _campaignRaw = 0;
            _scoreSubmitted = false;
            _finalScore = 0;
            _highlightIndex = -1;
            _lastRank = 0;
            StartMission();
        }

        private void StartMission()
        {
            _mission = Mission.All[_campaignIndex];
            _race = new Race(_mission, _difficulty, Environment.TickCount + _campaignIndex * 977);
            _countdown = 3.35f;
            _lastCountdownSecond = -1;
            _shake = 0f;
            _skyOffset = 0f;
            _radioText = "SUSPECT SPOTTED - GO!";
            _radioTimer = 2.4f;
            _audio.Play(_audio.SirenShort, 0.7f);
            SetScreen(GameScreen.Racing);
        }

        private void UpdateRacing(float dt)
        {
            if (_input.Pause)
            {
                _paused = true;
                SetScreen(GameScreen.Paused);
                return;
            }

            if (_race == null)
            {
                return;
            }

            _shake = MathF.Max(0f, _shake - dt * 3.5f);

            if (_countdown > 0f)
            {
                _countdown -= dt;
                int second = (int)MathF.Ceiling(_countdown);
                if (second != _lastCountdownSecond && second > 0)
                {
                    _lastCountdownSecond = second;
                    _audio.Play(_audio.Countdown, 0.7f);
                }

                if (_countdown <= 0f)
                {
                    _audio.Play(_audio.Countdown, 0.9f, 0.35f);
                    _lastCountdownSecond = 0;
                }

                _lastSpeedRatio = 0f;
                return;
            }

            var raceInput = new RaceInput
            {
                Steer = _input.Steer,
                Accelerate = !_input.Brake,
                Brake = _input.Brake,
                Turbo = _input.Turbo
            };

            _race.Update(dt, raceInput);
            _lastSpeedRatio = _race.SpeedRatio;
            _shake = MathF.Max(_shake, _race.Shake);

            float curve = _race.Road.CurveAt(_race.PlayerZ);
            _skyOffset += curve * _race.SpeedRatio * dt * 42f;

            HandleRaceEvents();

            if (_race.Busted)
            {
                _raceEndTimer += dt;
                if (_raceEndTimer > 2.2f)
                {
                    _campaignRaw += _race.RawScore;
                    _raceEndTimer = 0f;
                    _audio.Play(_audio.Checkpoint, 0.8f);
                    SetScreen(GameScreen.Busted);
                }
            }
            else if (_race.TimedOut)
            {
                _raceEndTimer += dt;
                if (_raceEndTimer > 1.2f)
                {
                    _campaignRaw += _race.RawScore;
                    _raceEndTimer = 0f;
                    _audio.StopRaceAudio();
                    SetScreen(GameScreen.GameOver);
                }
            }
            else if (_race.Wrecked)
            {
                _raceEndTimer += dt;
                if (_raceEndTimer > 2.2f)
                {
                    _campaignRaw += _race.RawScore;
                    _raceEndTimer = 0f;
                    _audio.StopRaceAudio();
                    SetScreen(GameScreen.GameOver);
                }
            }
            else
            {
                _raceEndTimer = 0f;
            }
        }

        private void HandleRaceEvents()
        {
            for (int i = 0; i < _race.Events.Count; i++)
            {
                RaceEvent evt = _race.Events[i];
                switch (evt.Type)
                {
                    case RaceEventType.Ram:
                        _shake = 1f;
                        _audio.Play(_audio.Ram, 0.9f);
                        SetRadio("RAMMED HIM! +" + evt.Value);
                        break;
                    case RaceEventType.TrafficHit:
                        _shake = 1f;
                        _audio.Play(_audio.Crash, 0.9f);
                        SetRadio(evt.Value > 0 ? "CRASH! HULL " + evt.Value : "CAR WRECKED");
                        break;
                    case RaceEventType.Checkpoint:
                        _audio.Play(_audio.Checkpoint, 0.7f);
                        SetRadio("CHECKPOINT - TIME EXTENDED");
                        break;
                    case RaceEventType.TurboEmpty:
                        SetRadio("TURBO EMPTY");
                        break;
                    case RaceEventType.OffRoad:
                        _audio.Play(_audio.Skid, 0.55f);
                        break;
                    case RaceEventType.SuspectEscaping:
                        _audio.Play(_audio.Wrong, 0.6f);
                        SetRadio("SUSPECT ESCAPING!");
                        break;
                    case RaceEventType.Gunfire:
                        _shake = MathF.Max(_shake, 0.5f);
                        _audio.Play(_audio.Crash, 0.35f);
                        SetRadio("SUSPECT IS SHOOTING!");
                        break;
                    case RaceEventType.PlayerHit:
                        _shake = 1f;
                        _audio.Play(_audio.Crash, 0.8f);
                        SetRadio("TAKING FIRE! HULL " + evt.Value);
                        break;
                    case RaceEventType.Wrecked:
                        _shake = 1f;
                        _audio.Play(_audio.Crash, 1f);
                        SetRadio("CAR WRECKED");
                        break;
                    case RaceEventType.NearMiss:
                        break;
                }
            }
        }

        private void SetRadio(string text)
        {
            _radioText = text;
            _radioTimer = 2.2f;
            _radioFlash = 1f;
        }

        private void UpdateRadio(float dt)
        {
            if (_radioTimer > 0f)
            {
                _radioTimer -= dt;
            }

            if (_radioFlash > 0f)
            {
                _radioFlash -= dt * 2f;
            }
        }

        private void UpdatePaused()
        {
            if (_input.Confirm || _input.Pause)
            {
                _paused = false;
                _audio.PauseRaceAudio(false);
                SetScreen(GameScreen.Racing);
            }
            else if (_input.Back)
            {
                _paused = false;
                _audio.PauseRaceAudio(false);
                _campaignRaw += _race?.RawScore ?? 0;
                SetScreen(GameScreen.GameOver);
            }
        }

        private void UpdateBusted()
        {
            if (_screenTime > 2.0f || _input.Confirm)
            {
                _campaignIndex++;
                if (_campaignIndex >= Mission.All.Count)
                {
                    _finalScore = DifficultyRules.Weight(_difficulty, _campaignRaw);
                    SetScreen(GameScreen.CampaignComplete);
                }
                else
                {
                    SetScreen(GameScreen.MissionCleared);
                }
            }
        }

        private void UpdateMissionCleared()
        {
            if (_screenTime > 1.2f && (_input.Confirm || _screenTime > 6f))
            {
                StartMission();
            }
        }

        private void UpdateCampaignComplete()
        {
            if (_screenTime > 1.0f && (_input.Confirm || _screenTime > 5.5f))
            {
                CompleteRun(_mission);
            }
        }

        private void UpdateGameOver()
        {
            if (_screenTime > 1.2f && (_input.Confirm || _screenTime > 6f))
            {
                CompleteRun(_mission);
            }
        }

        private void CompleteRun(Mission mission)
        {
            if (_scoreSubmitted)
            {
                SetScreen(GameScreen.HighScores);
                return;
            }

            int score = _finalScore > 0
                ? _finalScore
                : DifficultyRules.Weight(_difficulty, _campaignRaw);

            _finalScore = score;
            _pendingEntry = new ScoreEntry
            {
                Name = _lastName,
                Score = score,
                Difficulty = _difficulty,
                MissionId = mission?.Id ?? "",
                MissionTitle = mission?.Title ?? "",
                Rams = _race?.Rams ?? 0,
                Ticks = DateTime.UtcNow.Ticks
            };

            if (_scores.Table.Qualifies(score) || score > 0)
            {
                _nameLetters = new[]
                {
                    _lastName.Length > 0 ? char.ToUpperInvariant(_lastName[0]) : 'A',
                    _lastName.Length > 1 ? char.ToUpperInvariant(_lastName[1]) : 'A',
                    _lastName.Length > 2 ? char.ToUpperInvariant(_lastName[2]) : 'A'
                };
                _nameCursor = 0;
                SetScreen(GameScreen.NameEntry);
            }
            else
            {
                _scoreSubmitted = true;
                SetScreen(GameScreen.HighScores);
            }
        }

        private char[] _letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.!-".ToCharArray();

        private static Rectangle NameBox(int index)
        {
            return new Rectangle(107 + index * 38, 95, 30, 36);
        }

        private static Rectangle NameUpButton => new Rectangle(107, 80, 106, 13);
        private static Rectangle NameDownButton => new Rectangle(107, 134, 106, 13);
        private static Rectangle NameOkButton => new Rectangle(222, 95, 60, 36);

        private void UpdateNameEntry()
        {
            // On touch screens every part of name entry is an explicit, tappable control.
            bool touchUi = MobilePlatform || _PointerSeen;
            if (touchUi && _input.PointerPressed)
            {
                Point point = _input.Pointer.ToPoint();
                bool handled = false;

                for (int i = 0; i < 3; i++)
                {
                    if (NameBox(i).Contains(point))
                    {
                        _nameCursor = i;
                        _audio.Play(_audio.Select);
                        handled = true;
                    }
                }

                if (NameUpButton.Contains(point))
                {
                    ChangeNameLetter(1);
                    handled = true;
                }
                else if (NameDownButton.Contains(point))
                {
                    ChangeNameLetter(-1);
                    handled = true;
                }
                else if (NameOkButton.Contains(point))
                {
                    SubmitNameEntry();
                    return;
                }

                // The tap belongs to the name-entry UI even if it hit empty space.
                _input.Confirm = false;
                if (handled)
                {
                    return;
                }
            }

            if (_input.Left)
            {
                _nameCursor = (_nameCursor + 2) % 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Right)
            {
                _nameCursor = (_nameCursor + 1) % 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Up)
            {
                ChangeNameLetter(1);
            }

            if (_input.Down)
            {
                ChangeNameLetter(-1);
            }

            if (_input.Confirm)
            {
                SubmitNameEntry();
            }
        }

        private void ChangeNameLetter(int direction)
        {
            int current = Array.IndexOf(_letters, _nameLetters[_nameCursor]);
            current = (current + direction + _letters.Length) % _letters.Length;
            _nameLetters[_nameCursor] = _letters[current];
            _audio.Play(_audio.Select);
        }

        private void SubmitNameEntry()
        {
            string name = new string(_nameLetters);
            _pendingEntry.Name = name;
            _lastName = name;
            _lastRank = _scores.Submit(_pendingEntry);
            _highlightIndex = _lastRank - 1;
            _scoreSubmitted = true;
            _audio.Play(_audio.Beep, 0.9f, 0.2f);
            SetScreen(GameScreen.HighScores);
        }

        private static string ConfirmPrompt => MobilePlatform ? "TAP" : "FIRE";

        private void UpdateHighScores()
        {
            if (_screenTime > 0.5f && (_input.Confirm || _input.Back))
            {
                _audio.Play(_audio.Select);
                SetScreen(GameScreen.Title);
            }
        }

        private void PrepareSelfTest()
        {
            switch (_selfTestScreen)
            {
                case "splash":
                    SetScreen(GameScreen.Splash);
                    break;
                case "difficulty":
                    _difficultyCursor = 1;
                    SetScreen(GameScreen.DifficultySelect);
                    break;
                case "mission":
                    _missionCursor = 3;
                    SetScreen(GameScreen.MissionSelect);
                    break;
                case "briefing":
                    _missionCursor = 3;
                    _mission = Mission.All[3];
                    SetScreen(GameScreen.Briefing);
                    break;
                case "race":
                    _missionCursor = 0;
                    _difficulty = Difficulty.Beginner;
                    StartCampaign();
                    _countdown = 0f;
                    break;
                case "race2":
                    _missionCursor = 3;
                    _difficulty = Difficulty.Medium;
                    StartCampaign();
                    _countdown = 0f;
                    break;
                case "race3":
                    _missionCursor = 6;
                    _difficulty = Difficulty.Beginner;
                    StartCampaign();
                    _countdown = 0f;
                    break;
                case "briefing2":
                    _missionCursor = 6;
                    _mission = Mission.All[6];
                    SetScreen(GameScreen.Briefing);
                    break;
                case "pads":
                    _PointerSeen = true;
                    _missionCursor = 0;
                    _difficulty = Difficulty.Beginner;
                    StartCampaign();
                    _countdown = 0f;
                    break;
                case "busted":
                    _missionCursor = 0;
                    StartCampaign();
                    _countdown = 0f;
                    for (int i = 0; i < 90; i++)
                    {
                        _race.Update(1f / 60f, new RaceInput { Accelerate = true });
                    }

                    SetScreen(GameScreen.Busted);
                    break;
                case "gameover":
                    _missionCursor = 0;
                    StartCampaign();
                    _countdown = 0f;
                    _campaignRaw = 18450;
                    SetScreen(GameScreen.GameOver);
                    break;
                case "name":
                case "nametap":
                    _PointerSeen = true;
                    _pendingEntry = new ScoreEntry
                    {
                        Score = 9120,
                        Difficulty = Difficulty.Hard,
                        MissionId = "arctic",
                        MissionTitle = "FROSTLINE"
                    };
                    SetScreen(GameScreen.NameEntry);
                    break;
                case "scores":
                    _highlightIndex = 3;
                    SetScreen(GameScreen.HighScores);
                    break;
                case "touch":
                    RunTouchAudit();
                    SetScreen(GameScreen.Title);
                    break;
                default:
                    SetScreen(GameScreen.Title);
                    break;
            }
        }

        private void AuditTap(int x, int y)
        {
            _input = default;
            _input.Pointer = new Vector2(x, y);
            _input.PointerDown = true;
            _input.PointerPressed = true;
            // ReadInput turns the first frame of a touch into a confirm; emulate that here.
            _input.Confirm = true;
        }

        /// <summary>
        /// Drives a tap through every interactive screen and reports pass/fail. Run with
        /// <c>--selftest touch</c>; the audit is written next to the screenshot.
        /// </summary>
        private void RunTouchAudit()
        {
            _PointerSeen = true;
            var lines = new List<string>();
            int failures = 0;

            void Check(string name, bool passed)
            {
                lines.Add((passed ? "PASS  " : "FAIL  ") + name);
                if (!passed)
                {
                    failures++;
                }
            }

            SetScreen(GameScreen.Title);
            AuditTap(250, 198);
            UpdateTitle();
            Check("title: VIEW TABLE button", _screen == GameScreen.HighScores);

            _screenTime = 1f;
            AuditTap(160, 120);
            UpdateHighScores();
            Check("high scores: tap to return", _screen == GameScreen.Title);

            AuditTap(160, 100);
            UpdateTitle();
            Check("title: tap to deploy", _screen == GameScreen.DifficultySelect);

            AuditTap(160, 145);
            UpdateDifficultySelect();
            Check("skill: tap second row", _screen == GameScreen.MissionSelect && _difficultyCursor == 1);

            Rectangle card = MissionCards()[4];
            AuditTap(card.Center.X, card.Center.Y);
            UpdateMissionSelect();
            Check("mission: tap fifth card", _screen == GameScreen.Briefing && _missionCursor == 4);

            AuditTap(160, 120);
            UpdateBriefing();
            Check("briefing: tap to start", _screen == GameScreen.Racing);

            InputFrame pad = default;
            ApplyRacingTouch(ref pad, new Vector2(SteerLeftPad.Center.X, SteerLeftPad.Center.Y), true);
            Check("race: left pad steers left", pad.Steer < -0.9f);

            pad = default;
            ApplyRacingTouch(ref pad, new Vector2(SteerRightPad.Center.X, SteerRightPad.Center.Y), true);
            Check("race: right pad steers right", pad.Steer > 0.9f);

            pad = default;
            ApplyRacingTouch(ref pad, new Vector2(TurboPad.Center.X, TurboPad.Center.Y), true);
            Check("race: TURBO pad", pad.Turbo);

            pad = default;
            ApplyRacingTouch(ref pad, new Vector2(BrakePad.Center.X, BrakePad.Center.Y), true);
            Check("race: BRK pad", pad.Brake);

            pad = default;
            ApplyRacingTouch(ref pad, new Vector2(280, 130), true);
            Check("race: drag right steers", pad.Steer > 0.9f);

            SetScreen(GameScreen.Busted);
            AuditTap(160, 120);
            UpdateBusted();
            Check("busted: tap to continue", _screen == GameScreen.MissionCleared);

            _screenTime = 2f;
            AuditTap(160, 120);
            UpdateMissionCleared();
            Check("mission cleared: tap to continue", _screen == GameScreen.Racing);

            SetScreen(GameScreen.GameOver);
            _campaignRaw = 18450;
            _screenTime = 2f;
            AuditTap(160, 120);
            UpdateGameOver();
            Check("game over: tap to file the score", _screen == GameScreen.NameEntry);

            char before = _nameLetters[0];
            AuditTap(NameUpButton.Center.X, NameUpButton.Center.Y);
            UpdateNameEntry();
            Check("name entry: UP key changes the letter", _nameLetters[0] != before);

            AuditTap(NameBox(2).Center.X, NameBox(2).Center.Y);
            UpdateNameEntry();
            Check("name entry: letter box selects", _nameCursor == 2);

            AuditTap(NameOkButton.Center.X, NameOkButton.Center.Y);
            UpdateNameEntry();
            Check("name entry: OK submits", _screen == GameScreen.HighScores);

            SetScreen(GameScreen.Paused);
            AuditTap(160, 120);
            UpdatePaused();
            Check("pause: tap to resume", _screen == GameScreen.Racing);

            lines.Add(failures == 0
                ? "ALL " + lines.Count + " TOUCH CHECKS PASSED"
                : failures + " OF " + lines.Count + " TOUCH CHECKS FAILED");

            try
            {
                File.WriteAllLines(_selfTestPath + ".audit.txt", lines);
            }
            catch (Exception)
            {
                // Diagnostics must never stop the game.
            }

            Console.WriteLine(string.Join(Environment.NewLine, lines));
        }

        private void SeedSelfTestScores()
        {
            var entries = new[]
            {
                new { Name = "ACE", Score = 24800, Difficulty = Difficulty.Beginner, MissionId = "arctic" },
                new { Name = "NEO", Score = 19650, Difficulty = Difficulty.Medium, MissionId = "desert" },
                new { Name = "JAX", Score = 15200, Difficulty = Difficulty.Beginner, MissionId = "neon" },
                new { Name = "PC1", Score = 11400, Difficulty = Difficulty.Hard, MissionId = "alpine" },
                new { Name = "RIO", Score = 8750, Difficulty = Difficulty.Medium, MissionId = "coast" },
                new { Name = "MAX", Score = 6200, Difficulty = Difficulty.Beginner, MissionId = "harbour" },
                new { Name = "ZED", Score = 3100, Difficulty = Difficulty.Hard, MissionId = "neon" }
            };

            long ticks = DateTime.UtcNow.Ticks;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                _scores.Submit(new ScoreEntry
                {
                    Name = entry.Name,
                    Score = entry.Score,
                    Difficulty = entry.Difficulty,
                    MissionId = entry.MissionId,
                    MissionTitle = Mission.ById(entry.MissionId)?.Title ?? "",
                    Ticks = ticks + i
                });
            }
        }

        private void UpdateSelfTest(float dt)
        {
            // Feeds one real touch tap through the name-entry handler so the mobile keys can be
            // checked without a phone in hand.
            if (_selfTestScreen == "nametap" && !_selfTestTapped && _screenTime > 0.1f)
            {
                _selfTestTapped = true;
                InputFrame saved = _input;
                _input.Pointer = new Vector2(NameUpButton.Center.X, NameUpButton.Center.Y);
                _input.PointerPressed = true;
                _input.Confirm = false;
                UpdateNameEntry();
                _input = saved;
            }

            if (_screen == GameScreen.Racing && _race != null)
            {
                _input.Steer = MathF.Sin(_screenTime * 1.25f) * 0.3f;
                _input.Turbo = _screenTime > 1.2f;
                _input.Brake = false;
                UpdateRacing(dt);
                _audio.UpdateRaceAudio(_lastSpeedRatio, _race.OffRoad, false, _input.Turbo);
            }
        }

        private void CaptureSelfTest()
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var data = new Color[width * height];

            try
            {
                GraphicsDevice.GetBackBufferData(data);
                PngWriter.Write(_selfTestPath, width, height, data);
                File.WriteAllText(_selfTestPath + ".txt",
                    "screen=" + _screen + " size=" + width + "x" + height + Environment.NewLine);
            }
            catch (Exception ex)
            {
                File.WriteAllText(_selfTestPath + ".txt", "FAILED: " + ex.Message + Environment.NewLine);
            }

#if !IOS
            // iOS forbids programmatic termination; the harness only runs on desktop anyway.
            Exit();
#endif
        }

        // ===================================================================== input

        private void ReadInput()
        {
            var frame = new InputFrame();
            KeyboardState keys = Keyboard.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            frame.Left = keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A) || pad.DPad.Left == ButtonState.Pressed;
            frame.Right = keys.IsKeyDown(Keys.Right) || keys.IsKeyDown(Keys.D) || pad.DPad.Right == ButtonState.Pressed;
            frame.Up = keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W) || pad.DPad.Up == ButtonState.Pressed;
            frame.Down = keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S) || pad.DPad.Down == ButtonState.Pressed;

            frame.Steer = (frame.Right ? 1f : 0f) - (frame.Left ? 1f : 0f);
            frame.Steer += pad.ThumbSticks.Left.X;
            frame.Steer = Math.Clamp(frame.Steer, -1f, 1f);

            bool keyConfirm = keys.IsKeyDown(Keys.Enter) || keys.IsKeyDown(Keys.Space);
            bool padConfirm = pad.Buttons.A == ButtonState.Pressed || pad.Buttons.Start == ButtonState.Pressed;
            frame.Turbo = keys.IsKeyDown(Keys.Space) || keys.IsKeyDown(Keys.Z) || pad.Buttons.B == ButtonState.Pressed ||
                          pad.Buttons.X == ButtonState.Pressed;
            frame.Brake = keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.LeftControl) ||
                          pad.Triggers.Left > 0.2f;
            frame.Pause = keys.IsKeyDown(Keys.Escape) || keys.IsKeyDown(Keys.P) || pad.Buttons.Start == ButtonState.Pressed;
            frame.Back = keys.IsKeyDown(Keys.Escape) || pad.Buttons.B == ButtonState.Pressed;

            MouseState mouse = Mouse.GetState();
            bool mouseDown = mouse.LeftButton == ButtonState.Pressed;
            bool mousePressed = mouseDown && !_mouseWasDown;
            _mouseWasDown = mouseDown;

            Vector2 pointer = new Vector2(mouse.X, mouse.Y);
            bool pointerDown = mouseDown;
            bool pointerPressed = mousePressed;

            TouchCollection touches = TouchPanel.GetState();
            if (touches.Count > 0)
            {
                TouchLocation touch = touches[0];
                pointer = ToVirtual(touch.Position);
                pointerDown = true;
                pointerPressed = touch.State == TouchLocationState.Pressed;

                if (touch.State == TouchLocationState.Moved)
                {
                    pointerPressed = false;
                }

                // Detect the first frame of a new finger when the platform reports Moved.
                if (touch.State == TouchLocationState.Moved && _previousTouches.Count == 0)
                {
                    pointerPressed = true;
                }

                _PointerSeen = true;
                frame.Turbo |= MobilePlatform;
            }

            if (MobilePlatform && _PointerSeen)
            {
                // Name entry has its own on-screen keys, so a stray tap must not submit the
                // score before the player has had a chance to pick their letters.
                bool touchConfirms = _screen != GameScreen.NameEntry;
                frame.Confirm = keyConfirm || padConfirm || (pointerPressed && touchConfirms);
                if (pointerDown)
                {
                    frame.Turbo = false;
                }
            }
            else
            {
                frame.Confirm = keyConfirm || padConfirm || pointerPressed;
            }

            frame.Pointer = pointer;
            frame.PointerDown = pointerDown;
            frame.PointerPressed = pointerPressed;

            if (_screen == GameScreen.Racing && MobilePlatform && _PointerSeen)
            {
                ApplyRacingTouch(ref frame, pointer, pointerDown);
            }

            _previousTouches = touches;
            _input = frame;
        }

        private void ApplyRacingTouch(ref InputFrame frame, Vector2 pointer, bool down)
        {
            if (!down)
            {
                return;
            }

            Point point = pointer.ToPoint();

            if (TurboPad.Contains(point))
            {
                frame.Turbo = true;
            }
            else if (BrakePad.Contains(point))
            {
                frame.Brake = true;
                frame.Steer = 0f;
            }
            else if (SteerLeftPad.Contains(point))
            {
                frame.Steer = -1f;
            }
            else if (SteerRightPad.Contains(point))
            {
                frame.Steer = 1f;
            }
            else
            {
                // Drag steering anywhere on the lower screen, so a slightly missed pad still
                // turns the car instead of doing nothing.
                if (pointer.Y > 120)
                {
                    float centre = VirtualWidth / 2f;
                    frame.Steer = Math.Clamp((pointer.X - centre) / 70f, -1f, 1f);
                }
            }
        }

        private bool TapOn(Rectangle rectangle)
        {
            return _input.PointerPressed && rectangle.Contains(_input.Pointer.ToPoint());
        }

        private int TappedRow(int count, int startY, int rowHeight)
        {
            if (!_input.PointerPressed)
            {
                return -1;
            }

            int index = (int)((_input.Pointer.Y - startY) / rowHeight);
            if (index < 0 || index >= count)
            {
                return -1;
            }

            return index;
        }

        // ===================================================================== draw

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_target);
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);

            if (PortraitWindow)
            {
                DrawRotatePrompt();
            }
            else switch (_screen)
            {
                case GameScreen.Splash:
                    DrawSplash();
                    break;
                case GameScreen.Title:
                    DrawTitle();
                    break;
                case GameScreen.DifficultySelect:
                    DrawDifficultySelect();
                    break;
                case GameScreen.MissionSelect:
                    DrawMissionSelect();
                    break;
                case GameScreen.Briefing:
                    DrawBriefing();
                    break;
                case GameScreen.Racing:
                case GameScreen.Paused:
                    DrawRace();
                    break;
                case GameScreen.Busted:
                    DrawRace();
                    DrawBustedOverlay();
                    break;
                case GameScreen.MissionCleared:
                    DrawRace();
                    DrawMissionCleared();
                    break;
                case GameScreen.CampaignComplete:
                    DrawCampaignComplete();
                    break;
                case GameScreen.GameOver:
                    DrawGameOver();
                    break;
                case GameScreen.NameEntry:
                    DrawNameEntry();
                    break;
                case GameScreen.HighScores:
                    DrawHighScores();
                    break;
            }

            _spriteBatch.End();

            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(new Color(4, 4, 10));

            Rectangle destination = ComputeDestination();
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_target, destination, Color.White);
            _spriteBatch.End();

            if (_selfTest)
            {
                _selfTestFrame++;
                if (_selfTestFrame >= _selfTestFrames)
                {
                    CaptureSelfTest();
                }
            }

            base.Draw(gameTime);
        }

        private Rectangle ComputeDestination()
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            if (width <= 0 || height <= 0)
            {
                return new Rectangle(0, 0, VirtualWidth, VirtualHeight);
            }

            float scale = MathF.Min(width / (float)RenderSettings.Width, height / (float)RenderSettings.Height);
            int w = (int)(RenderSettings.Width * scale);
            int h = (int)(RenderSettings.Height * scale);
            return new Rectangle((width - w) / 2, (height - h) / 2, w, h);
        }

        private Vector2 ToVirtual(Vector2 screen)
        {
            // MonoGame hands back touches in TouchPanel display space (1280x960 on iOS), which is
            // not the resolution the picture is drawn at, so undo that scale before hit testing.
            Rectangle window = Window.ClientBounds;
            screen = TouchMapping.UndoTouchScale(screen, TouchPanel.DisplayWidth, TouchPanel.DisplayHeight,
                window.Width, window.Height);

            return TouchMapping.ToLogical(screen,
                GraphicsDevice.PresentationParameters.BackBufferWidth,
                GraphicsDevice.PresentationParameters.BackBufferHeight);
        }

        // ------------------------------------------------------------------ primitives

        private void Rect(int x, int y, int w, int h, Color color)
        {
            _spriteBatch.Draw(_white, new Rectangle(x * S, y * S, w * S, h * S), color);
        }

        /// <summary>Draws a baked texture using logical (320x240) coordinates.</summary>
        private void Image(Texture2D texture, int x, int y, int w, int h, Color? tint = null)
        {
            _spriteBatch.Draw(texture, new Rectangle(x * S, y * S, w * S, h * S), tint ?? Color.White);
        }

        private void Panel(int x, int y, int w, int h, Color fill, Color border)
        {
            Rect(x, y, w, h, fill);
            Rect(x, y, w, 2, border);
            Rect(x, y + h - 2, w, 2, border);
            Rect(x, y, 2, h, border);
            Rect(x + w - 2, y, 2, h, border);
        }

        private void Text(string text, int x, int y, Color color, int scale = 1)
        {
            _font.Draw(_spriteBatch, text, x, y, color, scale);
        }

        private void TextCentered(string text, int centerX, int y, Color color, int scale = 1)
        {
            _font.DrawCentered(_spriteBatch, text, centerX, y, color, scale);
        }

        /// <summary>Draws text word-wrapped to a character budget, one line per 10 logical pixels.</summary>
        private void TextWrapped(string text, int x, int y, int maxChars, Color color, int lineHeight = 10)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int start = 0;
            int line = 0;
            while (start < text.Length)
            {
                int length = Math.Min(maxChars, text.Length - start);
                if (start + length < text.Length)
                {
                    int cut = text.LastIndexOf(' ', Math.Min(text.Length - 1, start + length - 1), length);
                    if (cut > start)
                    {
                        length = cut - start;
                    }
                }

                Text(text.Substring(start, length).Trim(), x, y + line * lineHeight, color);
                start += length;
                while (start < text.Length && text[start] == ' ')
                {
                    start++;
                }

                line++;
                if (line > 4)
                {
                    break;
                }
            }
        }

        private void TextShadow(string text, int centerX, int y, Color color, int scale = 1)
        {
            _font.DrawCenteredShadowed(_spriteBatch, text, centerX, y, color, new Color(0, 0, 0, 190), scale);
        }

        private void Bar(int x, int y, int w, int h, float fill, Color back, Color front, Color border)
        {
            Rect(x, y, w, h, border);
            Rect(x + 1, y + 1, w - 2, h - 2, back);
            int inner = (int)((w - 2) * Math.Clamp(fill, 0f, 1f));
            if (inner > 0)
            {
                Rect(x + 1, y + 1, inner, h - 2, front);
            }
        }

        private bool Blink(float rate = 2f)
        {
            return ((int)(_time * rate)) % 2 == 0;
        }

        private void Background(MissionTheme theme)
        {
            ThemePalette palette = _art.Palette(theme);
            for (int y = 0; y < VirtualHeight; y++)
            {
                float t = y / (float)VirtualHeight;
                Rect(0, y, VirtualWidth, 1, Color.Lerp(palette.SkyTop, new Color(8, 8, 16), t * 0.75f));
            }
        }

        private void SirenStrip(int y)
        {
            int phase = (int)(_time * 8f) % 4;
            Color left = phase < 2 ? new Color(220, 40, 48) : new Color(60, 40, 48);
            Color right = phase < 2 ? new Color(40, 60, 120) : new Color(60, 120, 230);
            Rect(0, y, VirtualWidth / 2, 3, left);
            Rect(VirtualWidth / 2, y, VirtualWidth / 2, 3, right);
        }

        // ------------------------------------------------------------------ splash

        private void DrawSplash()
        {
            Background(MissionTheme.Harbour);
            Rect(0, 0, VirtualWidth, VirtualHeight, new Color(6, 8, 20, 235));

            Image(_art.SplashArt, 80, 36, 160, 96);
            SirenStrip(0);

            TextShadow("POLICE", 160, 132, new Color(250, 250, 255), 4);
            TextShadow("CHASER", 160, 162, new Color(250, 210, 80), 4);

            TextCentered("A 128K-STYLE PURSUIT", 160, 190, new Color(150, 170, 210), 1);
            TextCentered("ALL-THE-JOHNSONS", 160, 202, new Color(110, 120, 150), 1);

            float progress = Math.Clamp(_screenTime / 3.2f, 0f, 1f);
            Bar(60, 216, 200, 8, progress, new Color(18, 22, 34), new Color(240, 60, 60), new Color(70, 80, 110));

            if (Blink(3f))
            {
                TextCentered("LOADING", 160, 228, new Color(200, 206, 220), 1);
            }
        }

        private bool PortraitWindow =>
            GraphicsDevice.PresentationParameters.BackBufferHeight >
            GraphicsDevice.PresentationParameters.BackBufferWidth;

        private void DrawRotatePrompt()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            Panel(30, 74, 260, 92, new Color(8, 10, 26, 240), new Color(250, 210, 80));
            TextShadow("ROTATE DEVICE", 160, 88, new Color(250, 220, 120), 2);
            TextCentered("POLICE CHASER IS PLAYED IN LANDSCAPE", 160, 118, new Color(170, 190, 230));
            TextCentered("TURN THE DEVICE SIDEWAYS", 160, 132, new Color(150, 170, 210));
            TextCentered("THE 4:3 PICTURE SCALES TO FIT THE SCREEN", 160, 146, new Color(120, 140, 175));
        }

        // ------------------------------------------------------------------ title

        private void DrawTitle()
        {
            ThemePalette palette = _art.Palette(MissionTheme.Neon);
            for (int y = 0; y < VirtualHeight; y++)
            {
                float t = y / (float)VirtualHeight;
                Rect(0, y, VirtualWidth, 1, Color.Lerp(palette.SkyTop, palette.SkyBottom, t * t));
            }

            Image(_art.Sun(MissionTheme.Neon), 240, 8, 48, 48);
            Image(_art.Skyline(MissionTheme.Neon, false), 0, 54, 320, 96, Color.White * 0.9f);

            for (int i = 0; i < 10; i++)
            {
                Rect(0, 96 + i * 4, VirtualWidth, 2, new Color(20, 16, 40, 120));
            }

            SirenStrip(0);

            Image(_art.Badge, 6, 8, 26, 30);
            Image(_art.Badge, 288, 8, 26, 30);

            TextShadow("POLICE", 160, 40, new Color(250, 250, 255), 4);
            TextShadow("CHASER", 160, 70, new Color(250, 210, 80), 4);

            Panel(44, 112, 232, 40, new Color(8, 10, 26, 220), new Color(250, 210, 80));
            TextCentered("NINE MISSIONS - THREE SKILL LEVELS", 160, 118, new Color(160, 220, 255));
            TextCentered("SERIES II: ARMED SUSPECTS", 160, 132, new Color(250, 140, 140));

            if (Blink(2.2f))
            {
                TextCentered(ConfirmPrompt + " TO DEPLOY", 160, 164, Color.White);
            }

            if (MobilePlatform)
            {
                TextCentered("ON-SCREEN PADS IN RACE", 110, 176, new Color(120, 160, 210));
            }

            ScoreEntry best = _scores.Table.Best();
            if (best != null)
            {
                Text("HI " + best.Score.ToString("D6"), 8, 180, new Color(250, 210, 80));
                Text(best.Name + " " + DifficultyRules.Name(best.Difficulty), 8, 192, new Color(150, 160, 190));
            }

            Panel(202, 174, 112, 30, new Color(8, 10, 26, 210), new Color(80, 90, 130));
            Text("HIGH SCORES", 214, 180, new Color(190, 200, 230));
            Text("VIEW TABLE", 214, 192, new Color(130, 145, 180));
        }

        // ------------------------------------------------------------------ menus

        private Rectangle[] MissionCards()
        {
            var cards = new Rectangle[Mission.All.Count];
            for (int i = 0; i < cards.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                cards[i] = new Rectangle(6 + col * 104, 42 + row * 41, 100, 37);
            }

            return cards;
        }

        private void DrawDifficultySelect()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            TextShadow("SELECT SKILL", 160, 18, Color.White, 2);

            for (int i = 0; i < 3; i++)
            {
                Difficulty difficulty = DifficultyRules.All[i];
                bool selected = i == _difficultyCursor;
                int y = 82 + i * 42;

                Panel(24, y, 272, 36,
                    selected ? new Color(30, 40, 90) : new Color(12, 16, 34),
                    selected ? new Color(250, 210, 80) : new Color(60, 70, 110));

                Text(DifficultyRules.Name(difficulty), 34, y + 5, selected ? Color.White : new Color(190, 200, 220), 2);
                Text(DifficultyRules.Blurb(difficulty), 34, y + 24, new Color(150, 170, 210), 1);

                string points = difficulty == Difficulty.Beginner ? "FULL POINTS" :
                    difficulty == Difficulty.Medium ? "HALF POINTS" : "FIFTH POINTS";
                Text(points, 288 - _font.Measure(points, 1), y + 8, new Color(250, 210, 80), 1);

                if (selected)
                {
                    Text(">", 16, y + 10, new Color(250, 210, 80), 2);
                }
            }

            TextCentered("UP/DOWN TO CHOOSE - FIRE TO CONFIRM", 160, 214, new Color(130, 140, 175));
            TextCentered("M - MENU", 160, 228, new Color(90, 100, 130));
        }

        private void DrawMissionSelect()
        {
            Background(MissionTheme.Harbour);
            SirenStrip(0);

            TextShadow("SELECT MISSION", 160, 8, Color.White, 2);
            Text("SKILL: " + DifficultyRules.Name(_difficulty), 8, 28, new Color(250, 210, 80));

            Rectangle[] cards = MissionCards();
            for (int i = 0; i < cards.Length; i++)
            {
                Mission mission = Mission.All[i];
                ThemePalette palette = _art.Palette(mission.Theme);
                bool selected = i == _missionCursor;
                Rectangle card = cards[i];

                Panel(card.X, card.Y, card.Width, card.Height,
                    Color.Lerp(palette.BuildingDark, new Color(6, 8, 20), 0.35f),
                    selected ? new Color(250, 210, 80) : new Color(70, 80, 110));

                Rect(card.X + 3, card.Y + 3, 14, 10, palette.RumbleLight);
                Text((i + 1).ToString(), card.X + 6, card.Y + 5, new Color(20, 24, 40));

                if (mission.IsNewMission)
                {
                    bool series2 = mission.Series == 2;
                    int badgeWidth = series2 ? 16 : 26;
                    Rect(card.X + card.Width - badgeWidth - 3, card.Y + 3, badgeWidth, 10,
                        series2 ? new Color(250, 180, 60) : new Color(220, 60, 70));
                    TextCentered(series2 ? "II" : "NEW", card.X + card.Width - badgeWidth / 2 - 3, card.Y + 5,
                        series2 ? new Color(30, 20, 8) : Color.White);
                }

                TextCentered(CardTitle(mission), card.X + card.Width / 2, card.Y + 16, Color.White);
                TextCentered(ThemeLabel(mission.Theme), card.X + card.Width / 2, card.Y + 26,
                    new Color(170, 182, 210));
            }

            Mission current = Mission.All[_missionCursor];
            Panel(6, 164, 308, 62, new Color(6, 8, 20, 228), new Color(60, 70, 110));
            Text(current.Tagline, 12, 168, new Color(200, 210, 235));
            string detail = "SUSPECT " + current.SuspectName + " - " + current.SuspectCrime;
            Text(detail, 12, 180, new Color(250, 210, 80));
            Text("RAMS " + current.RequiredRams + "   TIME " + (int)current.BaseTimeSeconds + "S",
                12, 192, new Color(150, 170, 210));
            Text(current.SuspectArmed ? "SERIES II - ARMED SUSPECT - HULL AT RISK" : "SERIES I - TRAFFIC AND TIME ONLY",
                12, 204, current.SuspectArmed ? new Color(255, 120, 120) : new Color(150, 170, 210));

            TextCentered("ARROWS TO CHOOSE - FIRE TO START", 160, 232, new Color(90, 100, 130));
        }

        private static string CardTitle(Mission mission)
        {
            string title = mission.Title;
            int space = title.IndexOf(' ');
            return space > 0 ? title.Substring(0, space) : title;
        }

        private static string ThemeLabel(MissionTheme theme)
        {
            switch (theme)
            {
                case MissionTheme.Coast: return "COAST ROAD";
                case MissionTheme.Alpine: return "MOUNTAIN PASS";
                case MissionTheme.Neon: return "NIGHT CITY";
                case MissionTheme.Desert: return "SALT FLATS";
                case MissionTheme.Arctic: return "BLIZZARD";
                case MissionTheme.Industrial: return "FOUNDRY";
                case MissionTheme.Highway: return "INTERSTATE";
                case MissionTheme.Canyon: return "RED ROCK";
                default: return "DOCKS";
            }
        }

        private void DrawBriefing()
        {
            Background(_mission.Theme);
            SirenStrip(0);

            Panel(16, 22, 288, 172, new Color(8, 10, 26, 235), new Color(250, 210, 80));

            TextCentered("STAGE " + (_campaignIndex + 1) + "/" + Mission.All.Count + " - PURSUIT BRIEFING",
                160, 28, new Color(250, 210, 80));
            TextCentered(_mission.Title, 160, 42, Color.White, 2);

            TextWrapped(_mission.Tagline, 26, 64, 46, new Color(180, 195, 230));

            Text("SUSPECT:", 26, 90, new Color(140, 150, 185));
            Text(_mission.SuspectName, 86, 90, new Color(255, 140, 140), 2);
            Text("OFFENCE:", 26, 106, new Color(140, 150, 185));
            Text(_mission.SuspectCrime, 86, 106, new Color(200, 210, 235));

            Text("TARGET:", 26, 122, new Color(140, 150, 185));
            Text("RAM " + _mission.RequiredRams + " TIMES TO STOP THE CAR", 86, 122, new Color(200, 210, 235));

            Text("SKILL:", 26, 138, new Color(140, 150, 185));
            Text(DifficultyRules.Name(_difficulty), 86, 138, new Color(250, 210, 80));

            Text("TIME:", 26, 154, new Color(140, 150, 185));
            Text((int)DifficultyRules.BaseTime(_difficulty, _mission.BaseTimeSeconds) + " SECONDS", 86, 154,
                new Color(200, 210, 235));

            if (_mission.SuspectArmed)
            {
                Text("WARNING:", 26, 170, new Color(255, 120, 120));
                Text("SUSPECT IS ARMED - HULL AT RISK", 86, 170, new Color(255, 160, 160));
            }

            if (Blink(2f))
            {
                TextCentered("FIRE TO DEPLOY", 160, 182, Color.White);
            }

            Rect(0, 200, VirtualWidth, 24, new Color(10, 12, 24));
            TextCentered("STEER WITH LEFT/RIGHT - BRAKE DOWN - TURBO FIRE", 160, 206, new Color(140, 160, 200));
            TextCentered("MOBILE: ON-SCREEN PADS", 160, 217, new Color(100, 110, 140));
        }

        // ------------------------------------------------------------------ race

        private void DrawRace()
        {
            if (_race == null)
            {
                return;
            }

            ThemePalette palette = _art.Palette(_mission.Theme);

            float shakeAmount = (_shake * 3f + (_race.OffRoad ? 1.4f : 0f)) * S;
            float shakeX = MathF.Sin(_time * 90f) * shakeAmount;
            float shakeY = MathF.Cos(_time * 73f) * shakeAmount * 0.6f;

            BuildWorldSprites();

            _roadRenderer.SkylineOffset = _skyOffset;
            _roadRenderer.Render(_spriteBatch, _white, _art, _race.Road, palette, _mission.Theme,
                _race.PlayerZ, _race.PlayerX, _race.Road.HeightAt(_race.PlayerZ) + 1000f,
                shakeX, shakeY, _worldSprites);

            DrawWeather(palette);
            DrawPlayerCar(shakeX, shakeY);
            DrawHud();

            if (_screen == GameScreen.Racing && _countdown > 0f)
            {
                TextCentered("STAGE " + (_campaignIndex + 1) + " - " + _mission.Title, 160, 60,
                    new Color(200, 215, 245));

                int second = (int)MathF.Ceiling(_countdown);
                if (second > 0)
                {
                    TextShadow(second.ToString(), 160, 84, new Color(250, 210, 80), 6);
                }
                else
                {
                    TextShadow("GO!", 160, 84, new Color(120, 250, 140), 5);
                }
            }

            if (_screen == GameScreen.Paused)
            {
                Rect(0, 0, VirtualWidth, VirtualHeight, new Color(0, 0, 0, 170));
                TextShadow("PAUSED", 160, 100, Color.White, 4);
                TextCentered(MobilePlatform ? "TAP TO RESUME" : "FIRE TO RESUME - BACK TO QUIT", 160, 140,
                    new Color(190, 200, 230));
            }
        }

        private void DrawPlayerCar(float shakeX, float shakeY)
        {
            int baseY = 164 * S;
            int lean = (int)(_input.Steer * 4f) * S;
            int bump = (int)(MathF.Sin(_time * 18f) * 1.2f * Math.Clamp(_race.SpeedRatio, 0f, 1f) * S);

            Texture2D car = _input.Steer < -0.2f ? _art.PlayerCarLeft
                          : _input.Steer > 0.2f ? _art.PlayerCarRight
                          : _art.PlayerCar;
            int x = 160 * S - car.Width / 2 + lean + (int)(shakeX * 0.6f);
            int y = baseY + bump + (int)(shakeY * 0.6f);

            _spriteBatch.Draw(_art.Shadow, new Rectangle(x + 4 * S, y + 34 * S, 56 * S, 12 * S), Color.White);
            _spriteBatch.Draw(car, new Rectangle(x, y, car.Width, car.Height), Color.White);

            // Brake lights glow.
            if (_input.Brake)
            {
                _spriteBatch.Draw(_white, new Rectangle(x + 8 * S, y + 18 * S, 10 * S, 4 * S), new Color(255, 90, 90, 180));
                _spriteBatch.Draw(_white, new Rectangle(x + 46 * S, y + 18 * S, 10 * S, 4 * S), new Color(255, 90, 90, 180));
            }
        }

        private void BuildWorldSprites()
        {
            _worldSprites.Clear();

            Road road = _race.Road;
            float cameraZ = _race.PlayerZ;
            float horizon = RoadRenderer.DrawDistance * Road.SegmentLength;

            // Roadside props.
            IReadOnlyList<RoadProp> props = road.Props;
            for (int i = 0; i < props.Count; i++)
            {
                RoadProp prop = props[i];
                float z = prop.Segment * Road.SegmentLength;
                float dz = z - cameraZ;
                if (dz < 900f || dz > horizon)
                {
                    continue;
                }

                _worldSprites.Add(new WorldSprite
                {
                    Z = z,
                    X = prop.X,
                    Texture = _art.Prop(prop.Kind, prop.Variant),
                    WorldWidth = Art.PropWorldWidth(prop.Kind),
                    Tint = Color.White
                });
            }

            // Target car.
            Texture2D suspectTexture = _art.SuspectCars[Math.Clamp(_mission.SuspectColor, 0, _art.SuspectCars.Length - 1)];
            _worldSprites.Add(new WorldSprite
            {
                Z = _race.RivalZ,
                X = _race.RivalX,
                Texture = suspectTexture,
                WorldWidth = 760f,
                Tint = Color.White
            });

            // Traffic.
            for (int i = 0; i < _race.Traffic.Count; i++)
            {
                TrafficCar car = _race.Traffic[i];
                float dz = car.Z - cameraZ;
                if (dz < 900f || dz > horizon)
                {
                    continue;
                }

                _worldSprites.Add(new WorldSprite
                {
                    Z = car.Z,
                    X = car.X,
                    Texture = car.SameDirection ? _art.TrafficCars[car.Kind % _art.TrafficCars.Length]
                                               : _art.OncomingCars[car.Kind % _art.OncomingCars.Length],
                    WorldWidth = car.Kind == 2 ? 980f : 800f,
                    Tint = Color.White
                });
            }
        }

        private void DrawWeather(ThemePalette palette)
        {
            if (_mission.Weather == Weather.Clear)
            {
                return;
            }

            int count = _mission.Weather == Weather.Rain ? 80 : _mission.Weather == Weather.Snow ? 60 : 100;

            for (int i = 0; i < count; i++)
            {
                float seedX = (i * 97.3f) % VirtualWidth;
                float seedY = (i * 53.7f) % 200f;
                float speed = _mission.Weather == Weather.Sandstorm ? 220f : 180f;
                float x = seedX;
                float y = (seedY + _time * speed) % 220f;

                if (_mission.Weather == Weather.Rain)
                {
                    _spriteBatch.Draw(_art.RainDrop(), new Rectangle((int)(x * S), (int)(y * S), 2 * S, 8 * S), Color.White * 0.75f);
                }
                else if (_mission.Weather == Weather.Snow)
                {
                    float drift = MathF.Sin(_time + i) * 6f;
                    _spriteBatch.Draw(_art.SnowFlake(), new Rectangle((int)((x + drift) * S), (int)(y * S), 3 * S, 3 * S), Color.White);
                }
                else
                {
                    _spriteBatch.Draw(_white, new Rectangle((int)(x * S), (int)(y * S), 14 * S, S),
                        new Color(240, 210, 160, 90));
                }
            }

            if (_mission.Weather == Weather.Sandstorm)
            {
                Rect(0, 18, VirtualWidth, 190,
                    new Color(220, 180, 120, 30));
            }

            if (_mission.Weather == Weather.Snow)
            {
                Rect(0, 18, VirtualWidth, 190,
                    new Color(200, 220, 255, 18));
            }
        }

        private void DrawHud()
        {
            Rect(0, 0, VirtualWidth, 18, new Color(8, 10, 22));
            Rect(0, 18, VirtualWidth, 1, new Color(60, 70, 110));
            SirenStrip(0);

            Text("SCORE", 6, 4, new Color(140, 150, 185));
            Text(TotalScore().ToString("D6"), 44, 4, Color.White);

            string missionTitle = _mission.Title;
            TextCentered(missionTitle, 178, 4, new Color(250, 210, 80));

            float time = _race.TimeRemaining;
            Color timeColor = time < 10f && Blink(4f) ? new Color(255, 90, 90) : Color.White;
            Text("TIME", 262, 4, new Color(140, 150, 185));
            Text(((int)MathF.Ceiling(time)).ToString("D3"), 292, 4, timeColor);

            // Bottom console.
            Rect(0, 208, VirtualWidth, VirtualHeight - 208, new Color(8, 10, 22));
            Rect(0, 208, VirtualWidth, 1, new Color(60, 70, 110));

            int speedMph = (int)(_race.Speed / Race.MaxSpeed * 180f);
            Text("MPH", 6, 212, new Color(140, 150, 185));
            Text(speedMph.ToString("D3"), 26, 212, Color.White, 1);
            Bar(6, 222, 96, 8, _race.Speed / Race.TurboMaxSpeed, new Color(24, 28, 44),
                _race.SpeedRatio > 1f ? new Color(255, 160, 60) : new Color(90, 220, 140), new Color(60, 70, 110));

            Text("TURBO", 8, 231, new Color(140, 150, 185));
            Bar(44, 232, 52, 6, _race.Turbo, new Color(24, 28, 44), new Color(80, 150, 255), new Color(60, 70, 110));

            Text("SUSPECT", 112, 212, new Color(140, 150, 185));
            for (int i = 0; i < _race.RivalMaxHealth; i++)
            {
                int x = 112 + i * 13;
                Rect(x, 222, 11, 8, i < _race.RivalHealth ? new Color(230, 60, 60) : new Color(40, 44, 60));
            }

            int gap = (int)(_race.Gap / 10f);
            Text("HULL", 112, 232, new Color(140, 150, 185));
            for (int i = 0; i < _race.PlayerMaxHealth; i++)
            {
                Rect(134 + i * 10, 233, 7, 7,
                    i < _race.PlayerHealth ? new Color(90, 220, 140) : new Color(40, 44, 60));
            }

            Text("RANGE " + gap.ToString("D4") + "M", 134 + _race.PlayerMaxHealth * 10 + 5, 232,
                _race.Escaping ? new Color(255, 120, 120) : new Color(140, 150, 185));

            Text("STAGE", 250, 212, new Color(140, 150, 185));
            Text((_campaignIndex + 1) + "/" + Mission.All.Count, 250, 222, Color.White);

            Text("HI " + _scores.BestScore().ToString("D6"), 250, 232, new Color(250, 210, 80));

            if (_radioTimer > 0f && !string.IsNullOrEmpty(_radioText))
            {
                int w = _font.Measure(_radioText, 1) + 14;
                int x = 160 - w / 2;
                Panel(x, 24, w, 15, new Color(8, 10, 22, 230), new Color(250, 210, 80));
                TextCentered(_radioText, 160, 28,
                    _radioFlash > 0f ? Color.White : new Color(250, 220, 150), 1);
            }

            if (_PointerSeen && _screen != GameScreen.Paused && (MobilePlatform || _selfTest))
            {
                DrawTouchControls();
            }
        }

        // Touch pads live in the two bottom corners, under each thumb. The renderer and the hit
        // test share these rectangles so a button can never drift away from where it is drawn.
        private static readonly Rectangle SteerLeftPad = new Rectangle(4, 146, 60, 60);
        private static readonly Rectangle SteerRightPad = new Rectangle(68, 146, 60, 60);
        private static readonly Rectangle BrakePad = new Rectangle(198, 146, 48, 60);
        private static readonly Rectangle TurboPad = new Rectangle(250, 146, 64, 60);

        private void DrawTouchControls()
        {
            DrawTouchPad(SteerLeftPad, "<", 2);
            DrawTouchPad(SteerRightPad, ">", 2);
            DrawTouchPad(BrakePad, "BRK", 1);
            DrawTouchPad(TurboPad, "TURBO", 1);
        }

        private void DrawTouchPad(Rectangle rectangle, string label, int scale)
        {
            Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, new Color(10, 16, 32, 150));
            Rect(rectangle.X, rectangle.Y, rectangle.Width, 2, new Color(150, 185, 245, 210));
            Rect(rectangle.X, rectangle.Y + rectangle.Height - 2, rectangle.Width, 2, new Color(150, 185, 245, 210));
            Rect(rectangle.X, rectangle.Y, 2, rectangle.Height, new Color(150, 185, 245, 210));
            Rect(rectangle.X + rectangle.Width - 2, rectangle.Y, 2, rectangle.Height, new Color(150, 185, 245, 210));
            TextCentered(label, rectangle.X + rectangle.Width / 2,
                rectangle.Y + rectangle.Height / 2 - (scale > 1 ? 6 : 2), new Color(220, 235, 255, 240), scale);
        }

        private int TotalScore()
        {
            int raw = _campaignRaw + (_race?.RawScore ?? 0);
            return DifficultyRules.Weight(_difficulty, raw);
        }

        private void DrawBustedOverlay()
        {
            Rect(0, 0, VirtualWidth, VirtualHeight, new Color(0, 0, 0, 150));

            int wobble = (int)(MathF.Sin(_time * 12f) * 2f);
            Panel(36, 62 + wobble, 248, 104, new Color(8, 14, 30, 240), new Color(120, 240, 140));

            TextShadow("BUSTED!", 160, 70 + wobble, new Color(120, 250, 150), 4);
            TextCentered("SUSPECT " + _mission.SuspectName + " APPREHENDED", 160, 102 + wobble, Color.White);
            TextCentered("RAMS " + _race.Rams + "   +" + _race.RawScore.ToString("D5") + " PTS",
                160, 118 + wobble, new Color(250, 210, 80));

            if (Blink(3f))
            {
                TextCentered(ConfirmPrompt + " TO CONTINUE", 160, 138 + wobble, new Color(180, 200, 240));
            }
        }

        private void DrawMissionCleared()
        {
            Rect(0, 0, VirtualWidth, VirtualHeight, new Color(0, 0, 0, 170));
            Panel(30, 58, 260, 110, new Color(8, 14, 30, 245), new Color(250, 210, 80));

            TextShadow("MISSION CLEARED", 160, 68, new Color(250, 220, 120), 2);
            TextCentered(_mission.Title, 160, 92, Color.White);
            TextCentered("SCORE " + TotalScore().ToString("D6"), 160, 108, new Color(180, 220, 255), 1);

            Mission next = _campaignIndex + 1 < Mission.All.Count ? Mission.All[_campaignIndex + 1] : null;
            if (next != null)
            {
                TextCentered("NEXT: " + next.Title, 160, 126, new Color(250, 160, 160));
            }

            if (Blink(2.5f))
            {
                TextCentered(ConfirmPrompt + " TO CONTINUE", 160, 148, new Color(180, 200, 240));
            }
        }

        private void DrawCampaignComplete()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            Panel(20, 30, 280, 180, new Color(8, 10, 26, 240), new Color(250, 210, 80));
            TextShadow("ALL MISSIONS CLEARED", 160, 42, new Color(120, 250, 150), 2);
            TextCentered("THE CITY IS SAFE... FOR NOW", 160, 68, new Color(180, 200, 235));

            TextCentered("FINAL SCORE", 160, 92, new Color(140, 150, 185));
            TextCentered(DifficultyRules.Weight(_difficulty, _campaignRaw).ToString("D6"), 160, 104,
                new Color(250, 210, 80), 3);

            TextCentered("SKILL " + DifficultyRules.Name(_difficulty), 160, 136, Color.White);
            TextCentered("MISSIONS " + Mission.All.Count + "   RAMS " + TotalRams(), 160, 150,
                new Color(160, 180, 215));

            if (Blink(2f))
            {
                TextCentered(ConfirmPrompt + " TO FILE THE REPORT", 160, 176, new Color(200, 220, 250));
            }
        }

        private int TotalRams()
        {
            int rams = _race?.Rams ?? 0;
            return rams + Math.Max(0, _campaignIndex) * 3;
        }

        private void DrawGameOver()
        {
            Background(MissionTheme.Harbour);
            Rect(0, 0, VirtualWidth, VirtualHeight, new Color(0, 0, 0, 120));
            SirenStrip(0);

            // The game over card.
            Panel(26, 34, 268, 176, new Color(10, 12, 26, 245), new Color(220, 60, 70));
            Rect(26, 34, 268, 3, new Color(250, 210, 80));

            TextShadow("GAME OVER", 160, 46, new Color(250, 90, 90), 4);
            TextCentered("PURSUIT FAILED", 160, 80, new Color(200, 210, 235));

            if (_race?.Wrecked == true)
            {
                Text("CAUSE", 44, 100, new Color(130, 140, 175));
                Text("YOUR CAR WAS WRECKED", 44, 110, new Color(255, 160, 160));
            }
            else
            {
                Text("SUSPECT", 44, 100, new Color(130, 140, 175));
                Text(_mission.SuspectName + " GOT AWAY", 44, 110, new Color(255, 160, 160));
            }

            Text("MISSION", 44, 128, new Color(130, 140, 175));
            Text(_mission.Title, 44, 138, Color.White);

            Text("SCORE", 44, 156, new Color(130, 140, 175));
            Text(DifficultyRules.Weight(_difficulty, _campaignRaw).ToString("D6"), 44, 166,
                new Color(250, 210, 80), 2);

            Text("SKILL " + DifficultyRules.Name(_difficulty), 180, 100, new Color(160, 180, 215));
            Text("RAMS " + (_race?.Rams ?? 0), 180, 114, new Color(160, 180, 215));
            Text("CHECKPOINTS " + (_race?.Checkpoints ?? 0), 180, 128, new Color(160, 180, 215));

            if (Blink(2f))
            {
                TextCentered(ConfirmPrompt + " TO SEE THE HIGH SCORES", 160, 190, new Color(210, 225, 250));
            }
        }

        private void DrawNameEntry()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            Panel(24, 40, 272, 140, new Color(8, 10, 26, 240), new Color(250, 210, 80));
            TextShadow("NEW HIGH SCORE", 160, 48, new Color(250, 220, 120), 2);
            TextCentered("SCORE " + _pendingEntry.Score.ToString("D6"), 160, 68, Color.White);

            bool touchUi = MobilePlatform || _PointerSeen;

            if (touchUi)
            {
                DrawKey(NameUpButton, "UP");
            }

            for (int i = 0; i < 3; i++)
            {
                Rectangle box = NameBox(i);
                bool selected = i == _nameCursor;
                Panel(box.X, box.Y, box.Width, box.Height,
                    selected ? new Color(40, 50, 100) : new Color(14, 18, 34),
                    selected ? new Color(250, 210, 80) : new Color(70, 80, 110));
                TextCentered(_nameLetters[i].ToString(), box.X + box.Width / 2, box.Y + 9, Color.White, 2);
            }

            if (touchUi)
            {
                DrawKey(NameDownButton, "DOWN");
                DrawKey(NameOkButton, "OK");
                TextCentered("TAP A BOX - UP/DOWN CHANGES IT", 160, 153, new Color(150, 170, 210));
                TextCentered("OK ENTERS THE NAME", 160, 164, new Color(150, 170, 210));
            }
            else
            {
                TextCentered("UP/DOWN LETTER - LEFT/RIGHT MOVE", 160, 146, new Color(150, 170, 210));

                if (Blink(2f))
                {
                    TextCentered("FIRE TO CONFIRM", 160, 158, new Color(200, 220, 250));
                }
            }
        }

        private void DrawKey(Rectangle rect, string label)
        {
            Rect(rect.X, rect.Y, rect.Width, rect.Height, new Color(30, 38, 64, 220));
            Rect(rect.X, rect.Y, rect.Width, 1, new Color(120, 150, 210, 150));
            Rect(rect.X, rect.Y + rect.Height - 1, rect.Width, 1, new Color(120, 150, 210, 150));
            TextCentered(label, rect.X + rect.Width / 2, rect.Y + rect.Height / 2 - 3, new Color(200, 220, 250));
        }

        private void DrawHighScores()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            TextShadow("HALL OF FAME", 160, 8, new Color(250, 220, 120), 2);

            Panel(8, 28, 304, 186, new Color(8, 10, 26, 240), new Color(250, 210, 80));

            Text("RK", 16, 34, new Color(140, 150, 185));
            Text("NAME", 38, 34, new Color(140, 150, 185));
            Text("SCORE", 116, 34, new Color(140, 150, 185));
            Text("SKILL", 184, 34, new Color(140, 150, 185));
            Text("MISSION", 238, 34, new Color(140, 150, 185));
            Rect(14, 44, 292, 1, new Color(60, 70, 110));

            HighScoreTable table = _scores.Table;
            if (table.Entries.Count == 0)
            {
                TextCentered("NO SCORES YET - SET THE PACE", 160, 104, new Color(160, 170, 200));
            }

            for (int i = 0; i < table.Entries.Count && i < HighScoreTable.MaxEntries; i++)
            {
                ScoreEntry entry = table.Entries[i];
                int y = 50 + i * 14;
                bool highlight = i == _highlightIndex;
                if (highlight)
                {
                    Rect(12, y - 2, 296, 15, new Color(50, 40, 20));
                }

                Color rankColor = i == 0 ? new Color(250, 210, 80) : i == 1 ? new Color(210, 215, 230) : i == 2 ? new Color(220, 160, 100) : new Color(160, 170, 200);
                Text((i + 1).ToString("D2"), 18, y, rankColor);
                Text(entry.Name, 40, y, highlight ? Color.White : new Color(220, 225, 240));
                Text(entry.Score.ToString("D6"), 116, y, highlight ? new Color(250, 210, 80) : Color.White);
                Text(entry.DifficultyLabel, 184, y, new Color(160, 190, 230));
                Text(ShortMission(entry.MissionId), 238, y, new Color(150, 160, 190));
            }

            TextCentered("TOP 10 - SCORE IS WEIGHTED BY SKILL", 160, 200, new Color(140, 150, 185));
            if (Blink(2f))
            {
                TextCentered(ConfirmPrompt + " TO RETURN", 160, 218, Color.White);
            }
        }

        private static string ShortMission(string id)
        {
            switch (id)
            {
                case "harbour": return "HARBOUR";
                case "coast": return "COAST";
                case "alpine": return "ALPINE";
                case "neon": return "NEON";
                case "desert": return "DESERT";
                case "arctic": return "ARCTIC";
                case "steelworks": return "STEEL";
                case "interstate": return "INTERSTATE";
                case "canyon": return "CANYON";
                default: return "---";
            }
        }
    }
}
