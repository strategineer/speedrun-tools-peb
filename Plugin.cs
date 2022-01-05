using BepInEx;
using System.Diagnostics;
using System;
using UnityEngine;
using BepInEx.Configuration;
using MirrorInternalLogs;
using HarmonyLib;
using System.Reflection;

namespace com.strategineer.PEBSpeedrunTools
{

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("pigEatBallGame.exe")]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin
    {

        public static string[][] worldLevels = new string[][]
    {
        new string[]
        {
            "tennis_courts_a.prd",
            "tennis_courts_b.prd",
            "tennis_courts_c.prd",
            "tennis_courts_d.prd"
        },
        new string[]
        {
            "sushi_gardens_a.prd",
            "sushi_gardens_b.prd",
            "sushi_gardens_c.prd",
            "sushi_gardens_d.prd"
        },
        new string[]
        {
            "sports_barena_a.prd",
            "sports_barena_b.prd",
            "sports_barena_c.prd",
            "sports_barena_d.prd"
        },
        new string[]
        {
            "kitchen_chaos_alpha.prd",
            "kitchen_chaos_beta.prd",
            "kitchen_chaos_gamma.prd",
            "kitchen_chaos_main_hub.prd"
        },
        new string[]
        {
            "astro_farm_alpha.prd",
            "astro_farm_beta.prd",
            "astro_farm_gamma.prd",
            "astro_farm_main.prd"
        }
    };

        private static Harmony h;
        private static Stopwatch stopWatch = new Stopwatch();
        private static ConfigEntry<TextAnchor> _timerPosition;
        private static ConfigEntry<TextAnchor> _debugMsgPosition;
        private static ConfigEntry<bool> _showTimer;
        private static ConfigEntry<bool> _speedrunModeEnabled;

        const int MAX_STRING_SIZE = 499;

        private static readonly GUIStyle _textStyle = new GUIStyle();
        private static readonly GUIStyle _textShadowStyle = new GUIStyle();
        private static readonly GUIStyle _debugTextStyle = new GUIStyle();
        private static Rect _timerRect;
        private static Rect _timerShadowRect;
        private static Rect _debugMessageRect;
        private static string _lastDebugMessage = "PLACEHOLDER";
        private const int ScreenOffset = 10;
        private static bool isTimerOn = false;

        static void Log(string msg)
        {
            Console.WriteLine(msg);
            _lastDebugMessage = msg;
        }

        static void StartTimerIfNeeded(string reason)
        {
            Log($"PatchStartTimer: {reason}");
            if (!isTimerOn)
            {
                Log($"Starting timer because of {reason}");
                isTimerOn = true;
                stopWatch.Start();
            }
        }
        static void StopTimerIfNeeded(string reason)
        {
            Log($"PatchEndTimer: {reason}");
            if (isTimerOn)
            {
                Log($"Stopping timer because of {reason}");
                stopWatch.Stop();
                isTimerOn = false;
            }
        }
        static void ResetTimer(string reason)
        {       
            Log($"Resetting timer because of {reason}");
            stopWatch.Reset();
            isTimerOn = false;
        }

        [HarmonyPatch]
        class Patch1
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(LevelObj), nameof(LevelObj.MapLevelUnlockUpdate))]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.BeginFirstLevelFile))]
            [HarmonyPatch(typeof(MidGame), "FinishBeginMainGame")]
            [HarmonyPatch(typeof(LevelObj), nameof(LevelObj.Reset))]
            static void PatchStartTimer(MethodBase __originalMethod)
            {
                StartTimerIfNeeded(__originalMethod.Name);
            }
            [HarmonyPrefix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.StartTestLevelFromFile), new Type[] { typeof(LevelObj), typeof(string), typeof(string), typeof(string) })]
            static void PrefixStopTimer(MethodBase __originalMethod)
            {
                StopTimerIfNeeded(__originalMethod.Name);
            }

            // todo skip clam start chats and skip the leaderboard/award section

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuClamTalkStart), nameof(MenuClamTalkStart.DoStartup))]
            static void PostfixSkipClamTalk(ref MenuClamTalkStart __instance)
            {
                if (_speedrunModeEnabled.Value)
                {
                    Log($"Skipping clam talk");
                    // Just skip the clam talk and play the level
                    if (MidGame.staticMidGame.getCurrentMenu() is MenuClamTalkStart)
                    {
                        __instance.callFunctionDelayed(13);
                    }
                    // todo bug: because of this change, when we go back into a completed level the timer pauses during the item select screen
                }
            }


            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuWinScreen), nameof(MenuWinScreen.DoStartup))]
            static void PostfixSkipWinScreen(ref MenuWinScreen __instance, ref int ___currentState, ref float ___currentStateTime)
            {
                if (_speedrunModeEnabled.Value)
                {
                    Log("Skipping win screen");
                    // Just skip the win screen and play the next level or return to the world screen
                    ___currentStateTime = 100000f;
                    ___currentState = 42;
                    __instance.callFunction(new GenericButtonActioner(0, 0));
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(PigMenu), nameof(PigMenu.setSubMenu))]
            static void PostfixPigMenuSetSubMenu(ref PigMenu __instance, MenuBase ___currentSubMenu)
            {
                if (___currentSubMenu is MenuDisguises)
                {
                    StartTimerIfNeeded("Disguises Menu entered");
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuBase), nameof(MenuBase.setCurrentButton))]
            // This starts the timer if we're on a pause menu and then we select a different button
            // todo also figure out why the timer is paused when we enter into a clam level set
            static void PostfixMenuPauseSetCurrentButton(MenuBase __instance, ButtonBase ___lastButton, ButtonBase ___currentButton)
            {
                if (__instance is MenuPause || __instance is MenuPauseInGame)
                {
                    if (!isTimerOn
                        && ___lastButton != null
                        && ___currentButton != null
                        && ___lastButton != ___currentButton)
                    {
                        StartTimerIfNeeded("Movement detected in pause menu");
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.setCurrentMenu))]
            static void PatchSetCurrentMenu(ref MidGame __instance, MenuBase ___pauseMenu, MenuBase ___startGameMenu, MenuBase ___pauseMenuInGame, ref MenuBase ___currentMenu)
            {
                // this might not be right place for this but we should disable the leaderboards if we're speedrunning
                if (_speedrunModeEnabled.Value)
                {
                    PigEatBallGame.staticThis.gameSettings.enableLeaderboards = false;
                }
                if (___currentMenu == ___startGameMenu)
                {
                    // Initial game menu entered
                    // todo I can display info on my mod tools, saying that it's properly loaded here and then remove it
                    // when the menu changes
                    ResetTimer("Start game menu entered");
                }
                if(___currentMenu != null
                    && ___currentMenu is not MenuClamTalkStart
                    && ___currentMenu is not LevelStartScreen)
                {
                    StopTimerIfNeeded($"Menu/Dialog entered {___currentMenu}");
                } else
                {
                    StartTimerIfNeeded("Menu/Dialog exited");
                }
            }
        }

        private void Awake()
        {
            InternalUnityLogger.OnUnityInternalLog += HandleUnityLog;
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            _timerPosition = Config.Bind("Interface",
                "Timer position",
                TextAnchor.UpperLeft,
                "Which corner of the screen to display the timer in.");

            _debugMsgPosition = Config.Bind("Interface",
                "Debug Message position",
                TextAnchor.LowerRight,
                "Which corner of the screen to display the last debug message in.");

            _showTimer = Config.Bind("Interface",
                "Show Timer",
                true,
                "Should show the speedrun timer?");

            _speedrunModeEnabled = Config.Bind("Interface",
                "Speedrun Mode Enabled",
                true,
                "Should we try to automatically skip any dialogs?");
        }

        private void Start()
        {
            h = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        static void HandleUnityLog(object sender, UnityLogEventArgs e)
        {
            // do nothing but this might be useful later
        }

        private static void updateLooks()
        {

            _debugTextStyle.normal.textColor = _textStyle.normal.textColor = isTimerOn ? Color.yellow : Color.green;
            _textShadowStyle.normal.textColor = Color.black;

            int w = Screen.width, h = Screen.height;
            _debugMessageRect = _timerRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);
            _timerShadowRect = _timerRect;
            _timerShadowRect.x += 3;
            _timerShadowRect.y += 3;


            _textStyle.alignment = _textShadowStyle.alignment = _timerPosition.Value;
            _debugTextStyle.alignment = _debugMsgPosition.Value;
            _textStyle.fontSize = _textShadowStyle.fontSize = h / 40;
        }

        private void Update()
        {
            updateLooks();
        }

        private void displayTime()
        {
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime;
            if (ts.Hours > 0)
            {
                elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            } else
            {
                elapsedTime = String.Format("{0:00}:{1:00}.{2:00}",
                ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            }
            
            GUI.Label(_timerShadowRect, elapsedTime, _textShadowStyle);
            GUI.Label(_timerRect, elapsedTime, _textStyle);
            GUI.Label(_debugMessageRect, _lastDebugMessage, _debugTextStyle);
        }

        private void OnGUI()
        {
            displayTime();
        }
    }
}
