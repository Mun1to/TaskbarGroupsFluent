<p align="center">
  <img src="src/assets/Icon.ico" alt="Taskbar Groups Fluent" width="120" height="120" />
</p>

<h1 align="center">Taskbar Groups — Fluent</h1>

<p align="center">
  A modern WPF / .NET 8 rewrite of Taskbar Groups with a Fluent (WinUI&nbsp;3-style) interface.
  Group your shortcuts and pin them to the Windows taskbar.
</p>

---

## What is this

Taskbar Groups lets you bundle several shortcuts into a single icon that you pin
to the taskbar. Click the icon and a flyout opens with all the apps in that group.

This fork modernizes the original WinForms app:

- **Fluent design** — built on WPF + [WPF-UI](https://github.com/lepoco/wpfui): Mica
  backdrop, rounded corners, light/dark theme that follows the system.
- **Microsoft Store apps** — add UWP/MSIX apps that have no `.exe` (WhatsApp,
  Instagram, TikTok, …) straight from a built-in picker. These are the apps the
  classic file dialog can't reach.
- **.NET 8** — the whole interop core (shell links, icon extraction, taskbar
  positioning, UWP enumeration via WinRT) ported off .NET Framework 4.7.2.

## How to use

1. Open the app and click **Añadir grupo** (Add group).
2. Give it a name and an icon.
3. Add shortcuts: **Programa** (`.exe`/`.lnk`), **Carpeta** (folder) or **App Store**
   (installed UWP apps).
4. Save. The group's shortcut is created under
   `%AppData%\Jack Schierbeck\taskbar-groups\Shortcuts`.
5. Right-click that shortcut → **Pin to taskbar**. Click it to open the flyout.

## Architecture

| Project | Role |
| ------- | ---- |
| `TaskbarGroups.Core` | UI-agnostic logic: data model, shell/UWP/icon interop, paths |
| `TaskbarGroups.App` | Fluent editor — main window, group editor, Store app picker |
| `TaskbarGroups.Background` | Borderless flyout shown above the taskbar |

The app deploys the background flyout next to itself; a pinned shortcut launches it
with the group name as its argument.

## Building

Requires the .NET 8 SDK.

```bash
dotnet build TaskbarGroupsFluent.sln -c Release
```

The runnable app is `TaskbarGroups.App`.

## Credits

This project builds on the work of:

- [tjackenpacken/taskbar-groups](https://github.com/tjackenpacken/taskbar-groups) — the original app.
- [PikeNote/taskbar-groups-pike-beta](https://github.com/PikeNote/taskbar-groups-pike-beta) — community fork this rewrite is based on.

## License

[MIT](LICENSE), same as the projects above.
