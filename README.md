# speedrun-tools-peb

This is a mod/plugin for the obscure masterpiece that is [Pig Eat Ball](https://store.steampowered.com/app/339090/Pig_Eat_Ball/).

Nobody's done a speedrun of Pig Eat Ball due to its hidden gemness, so I built this plugin
to make speedrunning it feasible and plan on being the first to try!

If through some miracle you're reading this and you decide to speedrun this game, here's how you can use this plugin too.


## What does this plugin do?

- Adds an autosplitter for LiveSplit with support for real time and in-game time which starts and stops appropriately according the [recommendations laid out here](https://kb.speeddemosarchive.com/Making_your_game_speedrunner-friendly#The_timer)
	- for more consistent runs between players using more or less powerful PCs.
	- to allow runners to take small health breaks during a long run.
- Experimental menu skipping feature that skips through some menus (like the clam talking menu and the medal/pearl award menus).
	- Useful for wasting less time sitting in menus when praticing.
	- WARNING: this will break the story progression and prevent you from completing the game, disable it if you're planning on doing a full playthrough of the game.
- Cheats:
	- Use 1-7 alphanumeric keys to load into any world.
	- Press F5 to unlock all disguises and gain access to any world through Tube Junction.

## How do I install this plugin?
1. [Download the latest release of this plugin](https://github.com/strategineer/speedrun-tools-peb/releases).
2. Unzip into the game directory so that the BepInEx folder so that is next to pigEatBallGame.exe.
3. [Download the latest release of the LiveSplit Server component](https://github.com/LiveSplit/LiveSplit.Server/releases)
4. Unzip the file and move the .dll files into the Components folder in your LiveSplit install.
5. Open the PigEatBall.lss splits file in LiveSplit.
5. Play Pig Eat Ball and enjoy the auto splitter (You can click on LiveSplit, select 'Compare Against' -> Game Time to see the in-game timer)
6. [Optional] Press F1 to open an in-game config UI to change settings (mostly to turn the plugin on/off).