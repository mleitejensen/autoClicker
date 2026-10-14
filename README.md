# AutoClicker & TinyTask Macro Recorder

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
Chain steps that react to the on-screen bars: *Wait until a bar condition*, *Run a macro*, *Hold a key until a bar condition*, *Press a key*, *Click at a point*, *Wait a fixed time*, and *Go to step N if a bar condition*. Example: wait until Health = 50% (respawned) ? run move macro ? hold Shift until Energy = 80% ? press attack ? go to step 3 if Health = 10% ? click respawn. Bar conditions must hold for several consecutive reads (no single-frame flicker triggers them). Wait/Hold steps can also bail out early (*or if Health < X% go to step N*), and a Hold step that times out simply carries on. Hover a bar's region button to see its outline on screen; the colour button takes on the picked colour. The routine then repeats. Routines (steps, repeat count, bar regions/colors, tolerance) can be saved and opened as `.routine.json` files. The Play hotkey starts/stops it and F7 stops everything.
- **Universal Panic Key (F7)** – Instantly stops active auto-clicking, macro recording, or playback globally.
- **Always on Top** – Pin the window above other apps for easy access while gaming or working.
- **System Tray Integration** – Minimize or close to the system tray with a context menu to Start, Stop, Record, Play, Restore, or Exit.
- **Modern Dark UI** – Windows 10/11 immersive dark window styling with custom slate card layout and top mode switcher.
- **Persistent Settings** – All options, active tabs, positions, jitter settings, speeds, loops, and hotkeys automatically save to `%APPDATA%\AutoClicker\settings.json`.

## Requirements

- Windows 10 (version 1809+) or Windows 11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows Desktop)

## Build

```bash
dotnet build AutoClicker/AutoClicker.csproj -c Release
```

The compiled standalone executable is output to `AutoClicker/bin/Release/net8.0-windows/AutoClicker.exe`.

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

