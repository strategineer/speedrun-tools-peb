using BepInEx;
using System.Diagnostics;
using System;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace com.strategineer.PEBSpeedrunTools
{
    enum DebugTarget : int
    {
        STORY_STATE,
        TIMER_START,
        TIMER_STOP,
        TIMER_START_AND_STOP,
        LAST_AUTOSPLITTER_COMMAND
    }
    enum StoryState : int
    {
        STORY_STATE_STARTED = 0,
        STORY_STATE_WORLD1_INTERLUDE = 5,
        STORY_STATE_WORLD1_COMPLETE = 10,
        STORY_STATE_WORLD2_STARTED = 12,
        STORY_STATE_WORLD2_INTERLUDE = 15,
        STORY_STATE_WORLD2_COMPLETE = 20,
        STORY_STATE_WORLD3_STARTED = 22,
        STORY_STATE_WORLD3_INTERLUDE = 25,
        STORY_STATE_WORLD3_COMPLETE = 30,
        STORY_STATE_WORLD4_STARTED = 32,
        STORY_STATE_WORLD4_INTERLUDE = 35,
        STORY_STATE_WORLD4_COMPLETE = 40,
        STORY_STATE_WORLD5_STARTED = 42,
        STORY_STATE_WORLD5_INTERLUDE = 45,
        STORY_STATE_WORLD5_COMPLETE = 50,
        STORY_STATE_WORLD6_STARTED = 52,
        STORY_STATE_WORLD6_INTERLUDE = 55,
        STORY_STATE_WORLD6_COMPLETE = 60,
        STORY_STATE_GAME_ENDING1 = 100,
        STORY_STATE_GAME_ENDING2 = 110
    }

   
    class TextGUI
    {
        const int SCREEN_OFFSET = 10;
        const int SHADOW_OFFSET = 2;

        private string _text;
        private GUIStyle _style = new GUIStyle();
        private GUIStyle _shadowStyle = new GUIStyle();
        private Rect _rect;
        private Rect _shadowRect;

        private bool _shouldShow;

        private TextAnchor _anchor;
        public TextGUI() : this(TextAnchor.UpperLeft, Color.yellow, "") { }
        public TextGUI(TextAnchor anchor, Color color, string text)
        {
            _shouldShow = true;
            _text = text;
            _anchor = anchor;

            int w = Screen.width, h = Screen.height;
            _rect = _shadowRect = new Rect(SCREEN_OFFSET, SCREEN_OFFSET, w - SCREEN_OFFSET * 2, h - SCREEN_OFFSET * 2);
            _shadowRect.x += SHADOW_OFFSET;
            _shadowRect.y += SHADOW_OFFSET;

            _style.fontSize = _shadowStyle.fontSize = h / 40;
            SetAnchor(anchor);
            SetColor(color);
            SetShadowColor(Color.black);
        }

        public void SetColor(Color color)
        {
            _style.normal.textColor = color;
        }
        public void SetShadowColor(Color color)
        {
            _shadowStyle.normal.textColor = color;
        }

        public void SetAnchor(TextAnchor anchor)
        {
            _style.alignment = _shadowStyle.alignment = anchor;
        }

        public void SetText(string text)
        {
            this._text = text;
        }

        public void SetActive(bool shouldShow)
        {
            _shouldShow = shouldShow;
        }

        public void Draw()
        {
            if (_shouldShow)
            {
                GUI.Label(_shadowRect, _text, _shadowStyle);
                GUI.Label(_rect, _text, _style);
            }
        }
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("pigEatBallGame.exe")]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin
    {
        class AutoSplitter
        {
            // todo revaluate when the in-game timer should start and stop especially in menus (let's check with timo and speedrun.com, I'm sure people have discussed this)
            // todo test special logic to stop the timer and end the run
            // todo add code to set the story state artificially to test the last boss without playing through the whole game
            // detect STORY_STATE_GAME_ENDING2
            private Socket _socket;
            private bool _hasStarted = false;
            private bool _hasFinished = false;
            public enum Command
            {
                START,
                SPLIT,
                PAUSE,
                RESUME,
                RESET,
                INIT_GAMETIME,
                PAUSE_GAMETIME,
                UNPAUSE_GAMETIME
            }

            public void StartOrResume()
            {
                if (_hasFinished) return;
                if(!_hasStarted)
                {
                    _hasStarted = true;
                    Reset();
                    SendCommand(Command.START);
                    SendCommand(Command.INIT_GAMETIME);
                } else
                {
                    UnpauseGametime();
                }
            }
            public void FinishRun()
            {
                if (_hasFinished) return;
                PauseGametime();
                SendCommand(Command.PAUSE);
                _hasFinished = true;

            }
            public void UnpauseGametime()
            {
                if (_hasFinished) return;
                if (!_hasStarted) { return; }
                SendCommand(Command.UNPAUSE_GAMETIME);
            }

            public void Split()
            {
                if (_hasFinished) return;
                if (!_hasStarted) { return; }
                SendCommand(Command.SPLIT);
            }

            public void PauseGametime()
            {
                if (_hasFinished) return;
                if (!_hasStarted) { return; }
                SendCommand(Command.PAUSE_GAMETIME);
            }

            public void Reset()
            {
                if (_hasFinished) return;
                // todo I'm not sure this is working
                SendCommand(Command.RESET);
            }

            public AutoSplitter(string ip, int port)
            {
                try
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    IPAddress ipAdd = IPAddress.Parse(ip);
                    IPEndPoint remoteEP = new IPEndPoint(ipAdd, port);
                    _socket.Connect(remoteEP);
                }
                catch
                {
                    Log($"Can't create autosplitter with ip {ip} and port {port}");
                    throw;
                }
            }
            ~AutoSplitter()
            {
                if (_socket != null)
                {
                    _socket.Close();
                    _socket = null;
                }
            }

            private void SendCommand(Command command)
            {
                if (
                    _socket == null
                    || !_socket.Connected)
                {
                    Log("Could not send reset command to Live Split\nnot connected!");
                    return;
                }
                Debug(DebugTarget.LAST_AUTOSPLITTER_COMMAND, $"{Enum.GetName(typeof(Command), command)}");
                string msg;
                switch (command)
                {
                    case Command.START:
                        msg = "starttimer";
                        break;
                    case Command.SPLIT:
                        msg = "split";
                        break;
                    case Command.PAUSE:
                        msg = "pause";
                        break;
                    case Command.RESUME:
                        msg = "resume";
                        break;
                    case Command.RESET:
                        msg = "reset";
                        break;
                    case Command.INIT_GAMETIME:
                        msg = "initgametime";
                        break;
                    case Command.PAUSE_GAMETIME:
                        msg = "pausegametime";
                        break;
                    case Command.UNPAUSE_GAMETIME:
                        msg = "unpausegametime";
                        break;
                    default:
                        throw new Exception($"Missing command implementation {command}");
                }
                byte[] byData = Encoding.ASCII.GetBytes($"{msg}\r\n");
                _socket.Send(byData);
            }
        }

        const float FUZZ = 0.01f;
        const float BUFFER_TO_PREVENT_LEVEL_START_MENU_SKIP_IN_MS = 1000f;
        

        private static AutoSplitter _autoSplitter;
        private static HashSet<string> _enterClamSplits = new HashSet<string>();
        private static HashSet<string> _exitClamSplits = new HashSet<string>();

        private static Harmony _basicPatch = null;
        private static Harmony _timerPatch = null;
        private static Harmony _menuSkipsPatch = null;

        private static Stopwatch speedrunTimer = new Stopwatch();
        private static ConfigEntry<TextAnchor> _timerPosition;
        private static ConfigEntry<TextAnchor> _debugMsgPosition;
        private static ConfigEntry<bool> _showDebugText;
        private static ConfigEntry<DebugTarget> _debugTarget;

        private static ConfigEntry<bool> _liveSplitServerAutoSplitterEnabled;
        private static ConfigEntry<string> _liveSplitServerIP;
        private static ConfigEntry<int> _liveSplitServerPort;

        private static ConfigEntry<bool> _timerPatchEnabled;
        private static ConfigEntry<bool> _menuSkipsPatchEnabled;
        private static ConfigEntry<bool> _basicPatchEnabled;

        private static bool _gameLoaded = false;

        private static TextGUI _timerText = new TextGUI();
        private static Dictionary<DebugTarget, TextGUI> _debugTexts = new Dictionary<DebugTarget, TextGUI>();
        private static TextGUI _startupText = new TextGUI(TextAnchor.LowerCenter, Color.grey, $"strategineer's Pig Eat Ball Speedrun Tools version {PluginInfo.PLUGIN_VERSION} loaded.");
        private static bool _gameStarted = false;
        private static bool _movementDetectedOnPauseMenu = false;
        private static bool _isWarpingBetweenLevels = false;
        private static bool _isTalking = false;

        static void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        static void Debug(DebugTarget target, string msg)
        {
            _debugTexts[target].SetText($"{Enum.GetName(typeof(DebugTarget), target)}: {msg}");
        }

        static void StartTimerIfNeeded(string reason)
        {
            if (!speedrunTimer.IsRunning && _gameStarted)
            {
                Debug(DebugTarget.TIMER_START, reason);
                Debug(DebugTarget.TIMER_START_AND_STOP, reason);                
                Log($"Starting timer because of {reason}");
                speedrunTimer.Start();
                _autoSplitter.StartOrResume();
            }
        }
        static void StopTimerIfNeeded(string reason)
        {
            if (speedrunTimer.IsRunning && _gameStarted)
            {
                Debug(DebugTarget.TIMER_STOP, reason);
                Debug(DebugTarget.TIMER_START_AND_STOP, reason);
                Log($"Stopping timer because of {reason}");
                speedrunTimer.Stop();
                _autoSplitter.PauseGametime();
            }
        }
        static void ResetTimer(string reason)
        {
            Log($"Resetting timer because of {reason}");
            speedrunTimer.Reset();
            _autoSplitter.Reset();
        }

        [HarmonyPatch]
        class PatchBasic
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MidGame), "FinishBeginMainGame")]
            static void PostfixMidGameFinishBeginMainGame()
            {
                _gameStarted = true;
            }

            /// <summary>
            /// display info on my mod tools, saying that it's properly loaded here and then remove it
            ///   when the menu changesThis starts the timer if we're on a pause menu and then we select a different button
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuStartGame), nameof(MenuStartGame.LoadContent))]
            static void PostfixMenuStartGameLoadContent()
            {
                _gameLoaded = true;
            }
        }

        [HarmonyPatch]
        class PatchMenuSkips
        {

            /// <summary>
            /// Just skip talking to the clam and start playing the level right away
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuClamTalkStart), nameof(MenuClamTalkStart.DoStartup))]
            static void PostfixSkipClamTalk(ref MenuClamTalkStart __instance)
            {
                Log($"Skipping clam talk");
                if (MidGame.staticMidGame.getCurrentMenu() is MenuClamTalkStart)
                {
                    __instance.callFunctionDelayed(13);
                }
            }


            /// <summary>
            /// Just skip the win screen and play the next level or return to the world screen if we've beaten the level
            ///   Otherwise, let's restart the current level
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuWinScreen), nameof(MenuWinScreen.DoStartup))]
            static void PostfixSkipWinScreen(ref MenuWinScreen __instance, bool ___singlePlayerFailure, ref int ___currentState, ref float ___currentStateTime)
            {
                Log("Skipping win screen");
                ___currentStateTime = 100000f;
                ___currentState = 42;
                var stateNumber = ___singlePlayerFailure ? 1 : 0;
                // stateNumber 0 to move on, stateNumber 1 to restart the current level
                __instance.callFunction(new GenericButtonActioner(stateNumber, 0));
            }
        }

        [HarmonyPatch]
        class PatchInGameTimer
        {
            /// <summary>
            /// Split when we enter a clam for the first time.
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuClamTalkStart), "StartPlayLevel")]
            static void PostfixClamTalkStartPlayLevel()
            {
                MapTouchObj lastTouchObj = MidGame.staticMidGame.mapPlayerNew.lastTouchObj;
                string levelName = lastTouchObj.levelNodeNames[0];
                if (!_enterClamSplits.Contains(levelName))
                {
                    _enterClamSplits.Add(levelName);
                    _autoSplitter.Split();
                }
            }


            /// <summary>
            /// Split when we exit a clam having won the pearl inside for the first time.
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuWinScreen), nameof(MenuWinScreen.SetState))]
            static void PostfixMenuWinScreen(int newState)
            {
                if (newState == MenuWinScreen.STATE_SHUTDOWN)
                {
                    MapTouchObj lastTouchObj = MidGame.staticMidGame.mapPlayerNew.lastTouchObj;
                    string levelName = lastTouchObj.levelNodeNames[0];
                    bool wonPearl = MidGame.staticMidGame.LevelNodePearlWon(levelName);
                    if (wonPearl && !_exitClamSplits.Contains(levelName))
                    {
                        _exitClamSplits.Add(levelName);
                        _autoSplitter.Split();
                    }
                }
            }


            [HarmonyPostfix]
            [HarmonyPatch(typeof(MidGame), "FinishBeginMainGame")]
            static void PostfixMidGameFinishBeginMainGame()
            {
                PigEatBallGame.staticThis.gameSettings.enableLeaderboards = false;
            }

            /// <summary>
            /// This starts the timer if we're on a pause menu and then we select a different button
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuBase), nameof(MenuBase.setCurrentButton))]
            static void PostfixMenuPauseSetCurrentButton(MenuBase __instance, ButtonBase ___lastButton, ButtonBase ___currentButton)
            {
                if (
                    __instance is MenuPause
                    || __instance is MenuPauseInGame
                    || __instance is MenuDisguises)
                {
                    if (!speedrunTimer.IsRunning
                        && ___lastButton != null
                        && ___currentButton != null
                        && ___lastButton != ___currentButton)
                    {
                        _movementDetectedOnPauseMenu = true;
                    }
                }
            }


            [HarmonyPrefix]
            [HarmonyPatch(typeof(LevelStartScreen), nameof(LevelStartScreen.MenuUpdate))]
            static void PrefixLevelStartScreenDetectMovement(PigGameButton ___playButton, PigGameButton ___currentButton)
            {
                if (___playButton != ___currentButton) {
                    _movementDetectedOnPauseMenu = true;
                }
            }

            /// <summary>
            /// This starts the timer if we're on a pause menu and go into the disguises menu
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuBase), nameof(MenuBase.setSubMenu))]
            static void PostfixMenuPauseSetSubMenu(MenuBase __instance, MenuBase nextMenu)
            {
                if (!speedrunTimer.IsRunning
                    && __instance is MenuPause
                    && nextMenu is MenuDisguises)
                {
                    _movementDetectedOnPauseMenu = true;    
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Player), nameof(Player.InterLevelWarpStart))]
            static void PrefixInterLevelWarpStart()
            {
                _isWarpingBetweenLevels = true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(InterLevelWarp), "FinishLevelWarp")]
            static void PostfixInterLevelWarpFinish()
            {
                _isWarpingBetweenLevels = false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MenuCharacterTalkBase), "SetCharState")]
            static void PrefixMenuCharacterTalkBaseSetCharState(int newState)
            {
                if(newState == MenuCharacterTalkBase.CHAR_STATE_START)
                {
                    _isTalking = true;
                    StopTimerIfNeeded("We're talking!");
                } else
                {

                    _isTalking = false;
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.setCurrentMenu))]
            static void PrefixMidGameSetCurrentMenu(MenuBase _menu, ref MidGame __instance, MenuBase ___pauseMenu, MenuBase ___pauseMenuInGame, MenuBase ___startScreen, ref MenuBase ___currentMenu)
            {
                if (___currentMenu == ___pauseMenu
                    || ___currentMenu == ___pauseMenuInGame
                    || ___currentMenu == ___startScreen)
                {
                    if( _menu != ___currentMenu)
                    {
                        _movementDetectedOnPauseMenu = false;
                    }
                }
                if (_menu is MenuFront)
                {
                    ResetTimer("Back to front menu");
                    _gameStarted = false;
                }
            }
        }


        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");


            _basicPatchEnabled = Config.Bind("Features",
                "Enable basic features",
                true,
                "Should this plugin do anything");

            _basicPatchEnabled.SettingChanged += (sender, args) => UpdatePatches();

            _timerPatchEnabled = Config.Bind("Features",
                "Enable timer feature",
                true,
                "Enable in-game timer?");

            _timerPatchEnabled.SettingChanged += (sender, args) => {
                UpdateTextUIs();
                UpdatePatches();
            };

            _menuSkipsPatchEnabled = Config.Bind("Features",
                "Enable menu skipping feature",
                true,
                "Should we try to automatically skip some menus/dialogs?");

            _menuSkipsPatchEnabled.SettingChanged += (sender, args) => UpdatePatches();

            _timerPosition = Config.Bind("Interface",
                "Timer position",
                TextAnchor.UpperLeft,
                "Which corner of the screen to display the timer in.");

            _timerPosition.SettingChanged += (sender, args) => UpdateTextUIs();

            _debugMsgPosition = Config.Bind("Interface",
                "Debug Message position",
                TextAnchor.LowerRight,
                "Which corner of the screen to display the last debug message in.");

            _debugMsgPosition.SettingChanged += (sender, args) => UpdateTextUIs();

            _showDebugText = Config.Bind("Show/Hide",
                "Show Debug Text",
                false,
                "Should show the debug text?");

            _showDebugText.SettingChanged += (sender, args) => UpdateTextUIs();


            _debugTarget = Config.Bind("Features",
                "Debug Target",
                DebugTarget.STORY_STATE,
                "What should be displaying as the debug text?");

            _debugTarget.SettingChanged += (sender, args) => UpdateTextUIs();

            _liveSplitServerAutoSplitterEnabled = Config.Bind("Splits",
                "LiveSplit Server AutoSplits",
                false,
                "Should the plugin autosplit your LiveSplit?");

            _liveSplitServerAutoSplitterEnabled.SettingChanged += (sender, args) => UpdateLiveSplitServerSettings();

            _liveSplitServerIP = Config.Bind("Splits",
                "LiveSplit Server IP",
                "127.0.0.1",
                "The IP of your LiveSplit Server (127.0.0.1 if you're running it on the same PC)");

            _liveSplitServerIP.SettingChanged += (sender, args) => UpdateLiveSplitServerSettings();


            _liveSplitServerPort = Config.Bind("Splits",
                "LiveSplit Server Port",
                16834,
                "The Port of your LiveSplit Server (16834 by default)");

            _liveSplitServerPort.SettingChanged += (sender, args) => UpdateLiveSplitServerSettings();

            UpdateLiveSplitServerSettings();

        }


        static void UpdateLiveSplitServerSettings()
        {
            if (!_liveSplitServerAutoSplitterEnabled.Value) {
                _autoSplitter = null;
                return;
            }
            try
            {
                _autoSplitter = new AutoSplitter(_liveSplitServerIP.Value, _liveSplitServerPort.Value);
            }
            catch
            {
                // todo add error message displayed on screen for users
                Log($"Could not connect to Live Split server using IP {_liveSplitServerIP.Value} and port {_liveSplitServerPort.Value}");
            }
        }

        private void Start()
        {
            foreach (DebugTarget t in Enum.GetValues(typeof(DebugTarget)))
            {
                _debugTexts.Add(t, new TextGUI());
                Debug(t, "");
            }
            UpdatePatches();
            UpdateTextUIs();
        }

        private static void UpdateTextUIs()
        {
            if (_basicPatchEnabled.Value)
            {
                foreach(var d in _debugTexts)
                {
                    d.Value.SetAnchor(_debugMsgPosition.Value);
                    d.Value.SetActive(_showDebugText.Value && d.Key == _debugTarget.Value);
                }
            }
            if (_timerPatchEnabled.Value)
            {
                _timerText.SetAnchor(_timerPosition.Value);
                _timerText.SetActive(_timerPatchEnabled.Value);
            }
        }

        private void UpdatePatches()
        {
            if (_menuSkipsPatchEnabled.Value)
            {
                _menuSkipsPatch = Harmony.CreateAndPatchAll(typeof(PatchMenuSkips));
            }
            else
            {
                if (_menuSkipsPatch != null)
                {
                    _menuSkipsPatch.UnpatchSelf();
                    _menuSkipsPatch = null;
                }
            }
            if (_basicPatchEnabled.Value)
            {
                _basicPatch = Harmony.CreateAndPatchAll(typeof(PatchBasic));
            } else
            {
                if(_basicPatch != null)
                {
                    _basicPatch.UnpatchSelf();
                    _basicPatch = null;
                }

            }
            if (_timerPatchEnabled.Value)
            {
                _timerPatch = Harmony.CreateAndPatchAll(typeof(PatchInGameTimer));
            } else
            {
                if(_timerPatch != null)
                {
                    _timerPatch.UnpatchSelf();
                    _timerPatch = null;
                }

            }
        }
      
        private bool CheckLevelGameplayRunning()
        {
                return MidGame.staticMidGame != null
                && !MidGame.staticMidGame.GameplayPaused()
                && !MidGame.staticMidGame.LevelStartScreenActive()
                && MidGame.staticMidGame.getCurrentMenu() == null
                && !MidGame.staticMidGame.LevelGoalReached()
                && !MidGame.staticMidGame.winScreen.MenuActive();
        }


        private void CheckForCheats()
        {
            // Unlock everything
            if (Input.GetKeyUp(KeyCode.F5))
            {
                Log("Unlocking everything");
                MidGame.staticMidGame.UnlockAllBossSouvenirsCheat();
                for (int i = 0; i < 38; ++i)
                {
                    MidGame.playerProgress.DisguiseSetAcquired(i, true);
                }
                MidGame.staticMidGame.SaveGameData();
            }
            // Load any world
            int newWorldIndex = -1;
            int functionIndex = -1;
            if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                newWorldIndex = 3;
                functionIndex = 0;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha2))
            {
                newWorldIndex = 2;
                functionIndex = 1;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha3))
            {
                newWorldIndex = 1;
                functionIndex = 3;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha4))
            {
                newWorldIndex = 4;
                functionIndex = 4;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha5))
            {
                newWorldIndex = 5;
                functionIndex = 5;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha6))
            {
                newWorldIndex = 7;
                functionIndex = 6;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha7))
            {
                newWorldIndex = 8;
                functionIndex = 7;
            }

            if (newWorldIndex != -1)
            {
                string levName = MenuOptionsWorldCheat.worldLevelNames[functionIndex];
                Log($"Loading a new world: {levName}");
                MidGame.staticMidGame.LevelNodeBossSouvenirSet(1);
                MidGame.staticMidGame.LevelNodeBossSouvenirSet(4);
                MidGame.staticMidGame.LevelNodeBossSouvenirSet(5);
                MidGame.staticMidGame.LevelNodeBossSouvenirSet(7);
                MidGame.staticMidGame.SetPause(false);
                MidGame.worldIndexMap = 0;
                MidGame.areaIndexMap = 0;
                MidGame.staticMidGame.loadingScreenFrontMenuStart = true;
                MidGame.staticMidGame.loadingScreenWorldIndex = newWorldIndex;
                MidGame.staticMidGame.Difficulty(3f);
                MidGame.staticMidGame.publishTest = false;
                MidGame.testLoadUsed = false;
                MidGame.launchLevelType = MidGame.LAUNCH_TYPE.GAME;
                MidGame.staticMidGame.mapDisguiseType = 0;
                MidGame.staticMidGame.currentMapName = levName;
                MenuFront.StartGame(1, false, true, new MidGame.OnFinishBeginningLoad(() =>
                {
                    float zoomTarget = PigGameBase.camera2D.zoomTarget;
                    PigGameBase.camera2D.ZoomForce(0.5f);
                    PigGameBase.camera2D.ZoomTarget(zoomTarget);
                    MidGame.staticMidGame.Difficulty(3f);
                }));
                Log($"Finished loading a new world: {levName}");
            }
        }

        private void Update()
        {
            if (_basicPatchEnabled.Value)
            {
                CheckForCheats();
                if (MidGame.playerProgress != null)
                {
                    if (MidGame.playerProgress.storyState == (int)StoryState.STORY_STATE_GAME_ENDING2)
                    {
                        _autoSplitter.FinishRun();
                    }
                    Debug(DebugTarget.STORY_STATE, $"{((StoryState)MidGame.playerProgress.storyState).ToString()}");
                }
            }            
            if (_timerPatchEnabled.Value)
            {
                if (!_isWarpingBetweenLevels 
                    && !_isTalking
                    && (CheckLevelGameplayRunning() || _movementDetectedOnPauseMenu))
                {
                    StartTimerIfNeeded(_movementDetectedOnPauseMenu ? "Movement on pause menu" : "Gameplay");
                }
                else
                {
                    StopTimerIfNeeded("No gameplay or movement on pause menu");
                }
            }
        }

        private void OnGUI()
        {
            if (_gameLoaded)
            {
                TimeSpan ts = speedrunTimer.Elapsed;
                if (_timerPatchEnabled.Value)
                {
                    string elapsedTime;
                    if (ts.Hours > 0)
                    {
                        elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        ts.Hours, ts.Minutes, ts.Seconds,
                        ts.Milliseconds / 10);
                    }
                    else
                    {
                        elapsedTime = String.Format("{0:00}:{1:00}.{2:00}",
                        ts.Minutes, ts.Seconds,
                        ts.Milliseconds / 10);
                    }
                    _timerText.SetColor(speedrunTimer.IsRunning ? Color.green : Color.grey);
                    _timerText.SetText(elapsedTime);
                    _timerText.Draw();
                }
                if (_basicPatchEnabled.Value)
                {
                    // todo figure out a way to layout these so that they can be sorted and drawn at the same time
                    foreach (var d in _debugTexts.Values)
                    {
                        d.Draw();
                    }
                    if (ts.TotalSeconds < 5)
                    {
                        _startupText.Draw();
                    }
                }
            }
        }
    }
}
