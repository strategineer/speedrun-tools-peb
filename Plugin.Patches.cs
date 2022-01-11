using System;
using HarmonyLib;

namespace com.strategineer.PEBSpeedrunTools
{
    public partial class Plugin
    {
        [HarmonyPatch]
        class Patches
        {
            public class PatchBasic
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
            public class PatchMenuSkips
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
            public class PatchInGameTimer
            {

                [HarmonyPostfix]
                [HarmonyPatch(typeof(Player), nameof(Player.SetState))]
                static void PrefixPlayerSetState(int ___currentState)
                {
                    _playerState = ___currentState;
                }

                /// <summary>
                /// Split when we enter a clam for the first time.
                /// </summary>
                [HarmonyPostfix]
                [HarmonyPatch(typeof(MenuClamTalkStart), "StartPlayLevel")]
                static void PostfixClamTalkStartPlayLevel()
                {
                    MapTouchObj lastTouchObj = MidGame.staticMidGame.mapPlayerNew.lastTouchObj;
                    string levelName = lastTouchObj.levelNodeNames[0];
                    if (_autoSplitter != null)
                    {
                        _autoSplitter.SplitIfNeeded($"enter/{levelName}");
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
                        if (wonPearl && _autoSplitter != null)
                        {
                            _autoSplitter.SplitIfNeeded($"exit/{levelName}");
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
                        if (!inGameTimer.IsRunning
                            && ___lastButton != null
                            && ___currentButton != null
                            && ___lastButton != ___currentButton)
                        {
                            _overrideMenuTimer = true;
                        }
                    }
                }


                [HarmonyPostfix]
                [HarmonyPatch(typeof(LevelStartScreen), nameof(LevelStartScreen.SetState))]
                static void PrefixLevelStartScreenDetectMovement(int newState)
                {
                    if (newState == LevelStartScreen.STATE_SHOW_IDLE)
                    {
                        _idleOnLevelStartScreen = true;
                    }
                    else if (newState == LevelStartScreen.STATE_SHUTDOWN)
                    {
                        _idleOnLevelStartScreen = false;
                    }
                }

                /// <summary>
                /// This starts the timer if we're on a pause menu and go into the disguises menu
                /// </summary>
                [HarmonyPostfix]
                [HarmonyPatch(typeof(MenuBase), nameof(MenuBase.setSubMenu))]
                static void PostfixMenuPauseSetSubMenu(MenuBase __instance, MenuBase nextMenu)
                {
                    if (!inGameTimer.IsRunning
                        && __instance is MenuPause
                        && nextMenu is MenuDisguises)
                    {
                        _overrideMenuTimer = true;
                    }
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(Player), nameof(Player.InterLevelWarpStart))]
                [HarmonyPatch(typeof(MidGame), nameof(MidGame.BeginFirstWorldMapFile), new Type[] { typeof(LevelObj), typeof(MapTouchWorldNode), typeof(MidGame.OnFinishBeginningLoad) })]
                static void PrefixInterLevelWarpStart()
                {
                    _isWarpingBetweenLevels = true;
                }

                [HarmonyPostfix]
                [HarmonyPatch(typeof(InterLevelWarp), "FinishLevelWarp")]
                [HarmonyPatch(typeof(MenuPearls), "FinishLevelWarp")]
                static void PostfixInterLevelWarpFinish()
                {
                    _isWarpingBetweenLevels = false;
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(MenuCharacterTalkBase), "SetCharState")]
                static void PrefixMenuCharacterTalkBaseSetCharState(int newState)
                {
                    if (newState == MenuCharacterTalkBase.CHAR_STATE_START)
                    {
                        _isTalking = true;
                        StopTimerIfNeeded("We're talking!");
                    }
                    else
                    {

                        _isTalking = false;
                    }
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(MidGame), nameof(MidGame.setCurrentMenu))]
                static void PrefixMidGameSetCurrentMenu(MenuBase _menu, ref MidGame __instance, MenuBase ___pauseMenu, MenuBase ___pauseMenuInGame, MenuBase ___startScreen, ref MenuBase ___currentMenu)
                {
                    Debug(DebugTarget.CURRENT_MENU, $"{_menu}");
                    if (___currentMenu == ___pauseMenu
                        || ___currentMenu == ___pauseMenuInGame)
                    {
                        if (_menu != ___currentMenu)
                        {
                            _overrideMenuTimer = false;
                        }
                    }
                    if (_menu is MenuStartGame)
                    {
                        _gameStarted = false;
                        if (!inGameTimer.IsRunning)
                        {
                            ResetTimer("Arrived at front menu");
                        }
                        else
                        {
                            StopTimerIfNeeded("Returned to the front menu.");
                        }
                    }
                }
            }
        }
    }
}
