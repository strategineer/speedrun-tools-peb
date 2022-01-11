using BepInEx;
using System.Diagnostics;
using System;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;

namespace com.strategineer.PEBSpeedrunTools
{
    enum DebugTarget : int
    {
        GENERAL,
        CURRENT_MENU,
        PLAYER_STATE,
        STORY_STATE,
        TIMER_START,
        TIMER_STOP,
        TIMER_START_AND_STOP,
        LAST_AUTOSPLITTER_COMMAND,
        STORY_MOVIE_PLAYING
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
    // todo the menuwinscreen is a pain in the ass, it's so long, figure out a way to skip it properly
    // todo test logic to stop the timer and end the run and display the igt at the end
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("pigEatBallGame.exe")]
    public partial class Plugin : BaseUnityPlugin
    {
        private static AutoSplitter _autoSplitter;

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

        private static TextGUI _timerText = new TextGUI();
        private static Dictionary<DebugTarget, TextGUI> _debugTexts = new Dictionary<DebugTarget, TextGUI>();
        private static TextGUI _startupText = new TextGUI(TextAnchor.LowerCenter, Color.grey, $"strategineer's Pig Eat Ball Speedrun Tools version {PluginInfo.PLUGIN_VERSION} loaded.");

        private static bool _gameLoaded = false;
        private static bool _gameStarted = false;
        private static bool _overrideMenuTimer = false;
        private static bool _isWarpingBetweenLevels = false;
        private static bool _isTalking = false;
        private static bool _idleOnLevelStartScreen = false;
        private static int _playerState;
        private static int _lastStoryState;

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
                Debug(DebugTarget.TIMER_START_AND_STOP, $"start: {reason}");
                Log($"Starting timer because of {reason}");
                speedrunTimer.Start();
                if (_autoSplitter != null)
                {
                    _autoSplitter.StartOrResume();
                }
            }
        }
        static void StopTimerIfNeeded(string reason)
        {
            if (speedrunTimer.IsRunning && _gameStarted)
            {
                Debug(DebugTarget.TIMER_STOP, reason);
                Debug(DebugTarget.TIMER_START_AND_STOP, $"stop: {reason}");
                Log($"Stopping timer because of {reason}");
                speedrunTimer.Stop();
                if (_autoSplitter != null)
                {
                    _autoSplitter.PauseGametime();
                }
            }
        }
        static void ResetTimer(string reason)
        {
            Log($"Resetting timer because of {reason}");
            speedrunTimer.Reset();
            if (_autoSplitter != null)
            {
                _autoSplitter.Reset();
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
                false,
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
                true,
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
                _menuSkipsPatch = Harmony.CreateAndPatchAll(typeof(Patches.PatchMenuSkips));
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
                _basicPatch = Harmony.CreateAndPatchAll(typeof(Patches.PatchBasic));
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
                _timerPatch = Harmony.CreateAndPatchAll(typeof(Patches.PatchInGameTimer));
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
            Debug(DebugTarget.PLAYER_STATE, $"{_playerState}");
            if (_basicPatchEnabled.Value)
            {
                CheckForCheats();
                if (MidGame.playerProgress != null)
                {
                    if (_lastStoryState == (int)StoryState.STORY_STATE_GAME_ENDING1 
                        && MidGame.playerProgress.storyState == (int)StoryState.STORY_STATE_GAME_ENDING2)
                    {
                        if (_autoSplitter != null)
                        {
                            _autoSplitter.FinishRun();
                        }
                    }
                    _lastStoryState = MidGame.playerProgress.storyState;
                    Debug(DebugTarget.STORY_STATE, $"{((StoryState)MidGame.playerProgress.storyState).ToString()}");
                }
            }            
            if (_timerPatchEnabled.Value)
            {
                if(_playerState == Player.STATE_MENU_WAIT)
                {
                    StopTimerIfNeeded("In Menu Wait State");
                }
                else if(_idleOnLevelStartScreen)
                {
                    StartTimerIfNeeded("idle on level start screen");
                }
                else if(!_isWarpingBetweenLevels
                    && !_isTalking
                    && (CheckLevelGameplayRunning() || _overrideMenuTimer || MidGame.staticMidGame.getCurrentMenu() is MenuPearls))
                {
                    StartTimerIfNeeded($"warping? {_isWarpingBetweenLevels}, talking? {_isTalking}, player is in menu? {_playerState == Player.STATE_MENU_WAIT}, _overrideMenuTimer: {_overrideMenuTimer}");
                }
                else
                {
                    StopTimerIfNeeded($"warping? {_isWarpingBetweenLevels}, talking? {_isTalking}, player is in menu? {_playerState == Player.STATE_MENU_WAIT}, _overrideMenuTimer: {_overrideMenuTimer}");
                }
            }
        }

        private void OnGUI()
        {
            if (_gameLoaded)
            {
                TimeSpan ts = speedrunTimer.Elapsed;
                if (_autoSplitter != null
                    && _autoSplitter.IsFinished())
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
