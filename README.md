# Reflex

> Screen-aware auto clicker, TinyTask-style macro recorder and conditional routine runner.

A sleek, high-precision .NET 8 Windows Forms auto-clicker and input macro recorder designed with a modern dark theme and sub-millisecond timing accuracy.

## Features

### ⚡ Auto Clicker
- **Mouse Input** – Left, Right, or Middle click with **Single** or **Double-click** support.
- **Keyboard Automation** – Supports any key (letters, digits, arrows, navigation, function keys F1–F12, Space, Enter, Tab, Escape) with optional **Ctrl**, **Shift**, and **Alt** modifier key combinations.
- **Click Location Modes**:
  - **Dynamic Cursor** – Clicks wherever your mouse currently is.
  - **Fixed Coordinates (X, Y)** – Clicks at exact screen coordinates. Includes an interactive visual **Coordinate Picker** overlay.
  - **Position Jitter** – Adds human-like random coordinate dispersion (± pixels) around the target position.
- **High-Resolution Precision Timing** – Dedicated background loop utilizing Windows high-resolution performance counters to deliver accurate sub-millisecond intervals down to 1 ms without WinForms timer jitter.
- **Random Intervals** – Specify minimum and maximum intervals for natural, randomized delay variance.
- **Start Delay Countdown** – 0–60 second customizable delay to give you time to switch into your target window.
- **Hold Mode** – Press and continuously hold down a mouse button or key rather than repeatedly pulsing it.
- **Stop Conditions** – Automatically halt after reaching a set number of clicks or elapsed seconds.

### ⏺ TinyTask Input Macro Recorder & Playback
- **Universal Input Capture** – Records mouse movements, mouse button presses/releases (Left, Right, Middle, X1, X2), mouse wheel scrolling, and keyboard keystrokes using global low-level Windows hooks (`WH_MOUSE_LL` and `WH_KEYBOARD_LL`).
- **High-Fidelity Playback Engine** – High-resolution stopwatch timing reproduces actions with millisecond accuracy.
- **Variable Playback Speeds** – Run recorded sequences at **0.5x**, **1.0x (Normal)**, **2.0x**, **5.0x**, **10.0x**, or **Max** (instantaneous playback without delay).
- **Loop / Repeat Counts** – Repeat macros 1x, 2x, 3x, 5x, 10x, or set to **Continuous** loop until stopped.
- **Selective Mouse Movement Recording** – Option to record mouse movements or capture clicks and keystrokes only.
- **Emergency Safety Interruption** – "Pause on move" automatically aborts playback if you manually move the mouse (threshold: >25 px), releasing any held buttons or keys.
- **Macro Persistence** – Save and load macros as readable, portable JSON files (`.macro.json`).

### 🛠 General & System Integration
- **Configurable Global Hotkeys** – Dedicated hotkeys for Auto-Clicker Toggle (default **F6**), Stop All (default **F7**), Macro Record (default **F8**), and Macro Play (default **F9**), functional across all applications. Only the active tab's two hotkeys are registered: Clicker = Toggle/Stop, Macro = Record/Play, Routine = Play/Stop.
- **Screen Bars** � On the Routine tab, mark an Energy and/or Health bar on screen (region + fill color) for routine steps to read.

### ? Routine (conditional flows)
Chain steps that react to the on-screen bars: *Wait until a bar condition*, *Run a macro*, *Hold a key until a bar condition*, *Press a key*, *Click at a point*, *Wait a fixed time*, and *Go to step N if a bar condition*. Example: wait until Health = 50% (respawned) ? run move macro ? hold Shift until Energy = 80% ? press attack ? go to step 3 if Health = 10% ? click respawn. A step's condition isn't limited to one bar check: click *+ ADD CONDITION* to add more (e.g. Energy AND Health, or Energy OR Health), and choose whether ALL of them must hold at once or ANY single one is enough; each extra condition has its own ✕ button to remove it. Bar conditions must hold for several consecutive reads (no single-frame flicker triggers them). Wait/Hold steps can also bail out early (*or if Health < X% go to step N*), and a Hold step that times out simply carries on. Hover a bar's region button to see its outline on screen; the colour button takes on the picked colour. Click the colour button again to add further shades/colours the bar can show (e.g. a health bar that turns yellow then red as it drops) - each pixel only needs to match one of them; right-click the colour button to clear them and start over. The routine then repeats. Routines (steps, repeat count, bar regions/colors, tolerance) can be saved and opened as `.routine.json` files. The Play hotkey starts/stops it and F7 stops everything.
The *Read keys from screen and press them* step reads key prompts shown in a screen region (e.g. `A`, or `S D`) and presses them left to right. Pick the region so it tightly covers the prompt (hover the **REGION** button to see its outline on screen); for outlined game text also click **◉ LETTER COLOR** and then the *inside* colour of the letters (not the outline), so only the letters themselves are read. Use **▶ TEST** to see what is read. Glyphs are identified by looks you taught first (**✎ TEACH**: type the key(s) currently shown, e.g. `S D`, and each glyph is stored for its key), and by Windows OCR otherwise, so teaching is only needed for keys OCR misreads (stylised fonts, key-cap icons, `0`/`O`, `I`/`1`). Keys are only pressed once the same, fully readable prompt is seen twice in a row; *Give up after* sets how long to wait for a prompt (0 = try once), after which the routine carries on. Switch the **Letters / Arrows** dropdown to *Arrows* for prompts made of arrow badges: directions are recognised by shape (no teaching), the first badge that hasn't turned green yet is pressed, and the step waits for it to turn green before pressing the next, so a wrong or missed key is noticed. The keys pressed are shown in the status bar (`LAST: pressed …`). **Pause between presses** is the minimum time left between two keys; if the game still misses a press, the step automatically slows down for the rest of that prompt. Make the region wide enough for the longest prompt (4 badges).
The *Timing game* step automates "press a key when the moving marker is on the target" minigames (e.g. a white bar sliding over a gold zone that jumps to a new spot after each hit). Pick the **REGION** tightly around the strip, click **TARGET** on the zone (the screen freezes while you pick, so a moving marker or bar is no problem; click again for more shades, right-click to clear) and, if the marker isn't white, **MARKER** on the marker; choose the key and use **▶ TEST** to see the detected marker/target positions. It presses the key each time the marker is well inside the target (its whole width plus a margin at each end, so the press still lands on the target while the marker keeps moving), and ends after the chosen number of presses, once the target has been gone for ~3 s (it normally vanishes for under a second after each hit before the next one spawns), or at *Give up after*.
The *Ring game* step automates minigames where balls appear with a ring that closes onto them and must be clicked as the ring reaches them. Balls of any colour work: only the ring is recognised by colour. Pick the **REGION** to search (the whole game area is fine) and click **RING** two or three times on the ring (the lightest, middle and darkest parts; everything on the line between the picked colours counts as ring colour, so a gradient is covered). The screen freezes while you pick. **▶ TEST** reports the ring and the measured ball size. While running it tracks every ring many times a second, reads the ball's size from the edge inside the ring (or uses the *Ball radius* you enter), estimates how fast the ring is closing, and clicks the ball whose ring closes first, **Click … ms before the ring closes** ahead of time to cover input and frame delay (it can go negative, meaning later; getting "Good" because you are early means lowering it, late means raising it). The cursor is put on the ball with a real mouse-move event just beforehand, which games like Roblox need to notice the cursor has moved. Once a few balls have been clicked it learns the ball size (they are all the same size), and `%TEMP%\reflex-rings\log.txt` shows the value (`ball r=…`), which you can then type into *Ball radius*. Several balls at once are handled; it ends after the chosen number of balls, once no rings have been seen for ~3 s, or at *Give up after*. Balls whose colour is close to the ring's colour can confuse the detection. A log of detected rings and clicks is kept in `%TEMP%\reflex-rings\log.txt`.
- **Universal Panic Key (F7)** – Instantly stops active auto-clicking, macro recording, or playback globally.
- **Always on Top** – Pin the window above other apps for easy access while gaming or working.
- **System Tray Integration** – Minimize or close to the system tray with a context menu to Start, Stop, Record, Play, Restore, or Exit.
- **Modern Dark UI** – Windows 10/11 immersive dark window styling with custom slate card layout and top mode switcher.
- **Persistent Settings** – All options, active tabs, positions, jitter settings, speeds, loops, and hotkeys automatically save to `%APPDATA%\AutoClicker\settings.json`.

## Requirements

- **Windows 10 (version 1809+)** or Windows 11 (the on-screen key reader uses the built-in Windows OCR engine)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows Desktop)

## Build

```bash
dotnet build AutoClicker/AutoClicker.csproj -c Release
```

The compiled standalone executable is output to `AutoClicker/bin/Release/net8.0-windows10.0.19041.0/Reflex.exe`.

## Usage

### Auto Clicker Mode
1. **Input**: Choose **Mouse** (Left/Right/Middle, Single/Double click) or **Keyboard** (choose key and optional Ctrl/Shift/Alt modifiers).
2. **Location**: Select **Cursor** to click at the current mouse pointer or **Fixed (X, Y)** to click at set screen coordinates. Click **Pick** to visually select a point on your screen. Optionally enable **Position jitter** to add natural variance.
3. **Interval**: Enter the repeat interval in milliseconds (down to 1 ms), or tick **Random** to enter a Min/Max interval range.
4. **Timing & Limits**:
   - Set **Start delay (s)** to allow time to switch to your game or app before automation starts.
   - Tick **Hold** to continuously hold the input down instead of repeatedly clicking.
   - Tick **Stop after** to automatically stop after reaching a click count or elapsed seconds.
5. **Hotkeys & Safety**:
   - Configure your desired global **Toggle** and **Stop** hotkeys (default: **F6** and **F7**).
   - Toggle **Pause on move** for safety: moving your mouse by more than 15 px will immediately pause clicking.
6. Click **▶ Start** (or press **F6**) to begin. Click **■ Stop** or press **F7** at any time.

### TinyTask Macro Mode
1. Switch to the **⏺ Macro (TinyTask)** tab at the top.
2. Click **⏺ Record (F8)** (or press your record hotkey). Perform the mouse actions and keystrokes you want to automate.
3. Press **⏺ Stop (F8)** (or **F7**) to finish recording.
4. Adjust playback settings:
   - **Speed**: Choose between `0.5x` up to `Max`.
   - **Repeat**: Choose repeat iterations (e.g., `1x`, `5x`, or `Continuous`).
   - **Pause on move**: Halts playback automatically when you take over the mouse.
5. Click **▶ Play (F9)** to play back the recorded actions.
6. Click **💾 Save Macro...** to save the sequence to a `.macro.json` file, or **📂 Open Macro...** to load a previously saved macro.

