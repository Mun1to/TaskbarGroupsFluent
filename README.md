<div align="center">

<img src="brand/logo.png" width="128" height="128" alt="Taskbar Groups Fluent logo" />

# Taskbar Groups Fluent

**Group your shortcuts into a single taskbar icon.** A modern **WPF / .NET 10** rewrite of Taskbar Groups, with a Fluent (WinUI 3-style) interface.

🌍 [Español](README.es.md) · English

Website: **[tbgf.munito.dev](https://tbgf.munito.dev)**

<p>
  <a href="https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest">
    <img src="https://img.shields.io/github/v/release/Mun1to/TaskbarGroupsFluent?label=version&style=for-the-badge&color=0a84ff" alt="Latest release" />
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-0a84ff?style=for-the-badge" alt="MIT license" />
  </a>
  <img src="https://img.shields.io/badge/Windows%2010%20%2F%2011-x64-22c55e?style=for-the-badge" alt="Windows 10/11 x64" />
</p>

<p align="center">
  <a href="https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest/download/TaskbarGroupsFluent-Setup.exe">
    <img src="brand/download-button-en.svg" alt="Download Taskbar Groups Fluent" width="320" height="72" />
  </a>
</p>

<img src="docs/screenshots/panel-demo.gif" width="732" alt="Four groups pinned to a real Windows 11 taskbar, opened one after another: a grid, a list, a vertical column and a row" />

<sub>Recorded on a real Windows 11 taskbar: four groups, each with its own panel layout.</sub>

</div>

---

## ✨ What it does

Taskbar Groups turns a folder's worth of apps into **one taskbar button**. Click it and a small panel opens above it with every app in the group, so your taskbar stays tidy and everything you need is one click away.

- 🧩 **Add any installed app**: one searchable list of everything installed, desktop **and** Microsoft Store apps, read from Windows' own app catalog. A **Browse…** button covers anything that isn't listed.
- 🖼️ **The right icons**: they come from the same place the Start menu takes them from, sharp and transparent. When Windows has none for an app (some Electron apps), the app's own executable icon is used instead of a blank square.
- 🎨 **Your own group icon**: any image, cropped and zoomed in the built-in editor, or a **colour emoji**, or…
- 🔲 **…an icon made of the group's apps**: one click builds the taskbar icon from the first four apps in the group, and it keeps up as you add or remove them.
- 📌 **Pin to the taskbar**: each group becomes its own pinned button.
- 🪟 **Windows 11 glass, laid out your way**: a group's panel opens as a grid, a horizontal row, a vertical column or a list, with or without app names, in three icon sizes, with the group's name above or below. The editor shows a live preview as you change it.
- 🖱️ **Open on hover**: switch it on in the quick bar and a group opens when the cursor rests on its icon, no click needed. It waits a moment so crossing the taskbar never opens anything by accident, it never takes the keyboard away from what you were typing in, and it closes as soon as the cursor leaves. Off by default, because it keeps a small watcher in the notification area.
- 📋 **Right-click a pinned group** for **Open all** and **Edit group**.
- 🚀 **Open them all at once**, from that menu or from the button in the panel's header.
- 🗂️ **Groups inside groups**: add a group to another and it opens in its own panel.
- ⚙️ **Per-app options**: rename an app inside a group, give it launch arguments or run it as administrator.
- 📁 **Apps and folders** in the same group.
- 💾 **Export, import and duplicate**: share a group as a single file, keep a backup, or start a new group from a copy.
- 🔁 **Coming from Taskbar Groups or AppGroup?** Import your groups from either one and carry on.
- 🔄 **The taskbar icon follows your changes**: change a pinned group's icon and its button updates. Windows caches those icons, so this restarts Explorer once; your other pinned icons stay where they are.
- 🌗 **A quick bar at the bottom of the main window**: light or dark, English or Spanish (both follow Windows unless you pick one), open on hover, and the version you have.
- ⬇️ **Installer and updates**: install with a double click, and the app tells you when there is a new version and updates itself when you say so.

---

## 📸 Screenshots

<div align="center">
  <img src="docs/screenshots/en/main.png" width="820" alt="Main window with four groups and the quick bar at the bottom" />
</div>

<p align="center"><em>Your groups at a glance. The quick bar at the bottom holds the theme, the language, open on hover and the version, with a green tick when you are up to date.</em></p>

<div align="center">
  <img src="docs/screenshots/en/editor.png" width="420" alt="Group editor with the group's apps and a live preview of its panel" />
</div>

<p align="center"><em>The group editor: name, icon, apps, and a live preview of the panel as it will open.</em></p>

> 💡 Images on GitHub are clickable: open any screenshot to see it full size.

---

## ⬇️ Download & install

1. Click the **Download** button above, or use this direct link: **[download the installer](https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest/download/TaskbarGroupsFluent-Setup.exe)**.
2. Run `TaskbarGroupsFluent-Setup.exe`. It installs for your user only (no admin needed) and, if the **.NET 10 Desktop Runtime** is missing, downloads and installs it.
3. Open **Taskbar Groups** from the Start menu.

> Installing on a PC without internet? `TaskbarGroupsFluent-Setup-Offline.exe`, on the [Releases page](https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest), carries the .NET runtime inside.

> Windows may warn about an *"unknown publisher"*, because the app isn't signed with a paid certificate. Click **More info → Run anyway**.

## 🔄 Updates

On launch, the app checks GitHub for a newer version. The bottom right corner of the main window shows the version you have, with a green tick when you are up to date (click it to check again). When there is a new one it says **Update now**: one click downloads it, installs it and reopens the app.

---

## 🚀 How to use it

1. Click **Add group** and give it a **name** and an **icon** (an image, an emoji, or **Apps** to build it from the group's apps).
2. Add shortcuts with **App** (any installed app from the searchable list, or *Browse…*), **Folder** or **Group**.
3. Choose how its panel looks under **Panel**, and click **Save group**.
4. On the group's card, click **Pin to taskbar**. The app opens the folder with the group's shortcut highlighted: right-click it, then *Show more options*, then *Pin to taskbar*. Windows 11 doesn't let apps pin themselves, so this last step is yours.
5. Click the pinned button to open the panel with your apps, or right-click it for **Open all** and **Edit group**.

---

## 🛠️ Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/Mun1to/TaskbarGroupsFluent.git
cd TaskbarGroupsFluent
dotnet build TaskbarGroupsFluent.sln -c Release
```

Run the `TaskbarGroups.App` project. To produce the folder the installer packs:

```bash
dotnet publish src/TaskbarGroups.App -c Release -r win-x64 --self-contained false -o dist/TaskbarGroupsFluent
dotnet publish src/TaskbarGroups.Hover -c Release -r win-x64 --self-contained false -o dist/TaskbarGroupsFluent
dotnet publish src/TaskbarGroups.Background -c Release -r win-x64 --self-contained false -o dist/TaskbarGroupsFluent/Background
```

The installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php) from [`installer/TaskbarGroupsFluent.iss`](installer/TaskbarGroupsFluent.iss).

### 🧱 How it works

| Project | Role |
| --- | --- |
| `TaskbarGroups.Core` | UI-agnostic logic: data model, Windows' app catalog and icons, shell interop, paths |
| `TaskbarGroups.App` | The Fluent app: main window, group editor, app and emoji pickers, icon crop editor, updater |
| `TaskbarGroups.Background` | The borderless panel shown above the taskbar |
| `TaskbarGroups.Hover` | Optional watcher for open on hover, living in the notification area |

Each pinned group is a shortcut to the panel with the group's name as its argument, and its own AppUserModelID, which is what keeps every group as a separate taskbar button.

Hovering is not something Windows offers: a taskbar button reports nothing when the cursor is over it, so the watcher reads the taskbar's buttons through UI Automation and checks the cursor against them. It only does that while the cursor is actually on the taskbar, and caches what it reads, which keeps it at a fraction of a percent of one core while idle.

Opening quickly takes one more trick. Starting a panel from cold means loading .NET, then WPF, then painting a window for the first time, and that is most of a second however little the panel does. So as the cursor comes near the taskbar the watcher loads one in advance and keeps it hidden, ready to be shown in about fifty milliseconds; a loaded panel holds real memory, so it is let go once the cursor has been away for a while.

## 🙏 Credits

Built on the work of:

- [tjackenpacken/taskbar-groups](https://github.com/tjackenpacken/taskbar-groups), the original app.
- [PikeNote/taskbar-groups-pike-beta](https://github.com/PikeNote/taskbar-groups-pike-beta), the community fork whose structure this rewrite started from.
- [WPF-UI](https://github.com/lepoco/wpfui), the Fluent control library, and [SkiaSharp](https://github.com/mono/SkiaSharp), for colour emoji.

## 🔍 Don't trust it, check it

Open source only helps if somebody actually reads the code, and almost nobody does. So
instead of asking you to trust this project, here is the prompt to check it: point your own
AI agent at this repository and get a security report, in your language, in a few minutes,
even if you do not know how to program.

**[Open AI-AUDIT.md](AI-AUDIT.md)** and paste it into Claude Code, Codex, Cursor, Copilot or
whatever you use. It is the same prompt in every public repository here, so you can compare.

> **ES:** No hace falta que te fíes. Abre [AI-AUDIT.md](AI-AUDIT.md), pega ese texto en tu IA
> y te dirá en tu idioma qué hace este programa de verdad: qué envía por internet, qué toca
> en tu ordenador y qué ejecuta al instalarse.

## 📜 License

[MIT](LICENSE), same as the projects above.
