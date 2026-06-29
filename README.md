# AutoClicker

A .NET 8 Windows Forms auto-clicker that lets you repeatedly press any mouse button or keyboard key at a configurable interval.

## Features

- **Mouse buttons** – Left, Right, or Middle click
- **Keyboard keys** – any letter, digit, arrow key, function key (F1–F12, except F6/F7 which are hotkeys), Space, Enter, Tab, Escape
- **Configurable interval** – 10 ms to 60 000 ms (1 minute)
- **Global hotkeys** – press **F6** to start and **F7** to stop from any application

## Requirements

- Windows 10 or later
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build

```bash
cd AutoClicker
dotnet build -c Release
```

The executable is output to `AutoClicker/bin/Release/net8.0-windows/AutoClicker.exe`.

## Usage

1. Run `AutoClicker.exe`.
2. Choose **Mouse Button** or **Keyboard Key**.
3. Select the desired button/key from the dropdown.
4. Set the **Interval** (milliseconds between each click/press).
5. Click **▶ Start (F6)** or press **F6** globally to begin.
6. Click **■ Stop (F7)** or press **F7** globally to stop.
