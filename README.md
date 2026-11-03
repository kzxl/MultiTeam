# MultiTeam Hub

<p align="center">
  <img src="app.png" alt="MultiTeam Hub Logo" width="128" height="128">
</p>

<p align="center">
  <strong>High-Performance, Isolated Multi-Account Workspace Client for Desktop Messaging Platforms</strong>
</p>

<p align="center">
  Built with C# .NET 8 (WPF) and Microsoft Edge WebView2 (Chromium).
</p>

---

## Overview

**MultiTeam Hub** is a lightweight Windows desktop client designed to manage and switch seamlessly between multiple chat and enterprise workspace accounts simultaneously without session conflicts or credential leakage.

Each account runs within a dedicated, isolated Chromium profile (`userDataFolder`), ensuring 100% independence for cookies, LocalStorage, IndexedDB, and persistent logins.

---

## Key Features

- **Multi-Account Profile Isolation**: Dedicated `userDataFolder` per account. Log into multiple personal, team, or client accounts simultaneously without session collision.
- **10 Supported Platforms & Custom Web Apps**:
  - Microsoft Teams
  - Zalo Web
  - Telegram Web
  - Facebook Messenger
  - WhatsApp Web
  - Discord
  - Slack
  - Skype Web
  - Google Chat
  - Custom URLs (internal enterprise web tools, portals)
- **Profile Grouping & Color Tagging**:
  - Organize accounts into collapsible accordion groups (e.g., Work, Sales, Personal, VIP).
  - 8-color tagging palette with colored vertical indicator stripes for quick visual recognition.
  - Aggregate unread badge counters per group and per account.
- **Bilingual UI (Vietnamese 🇻🇳 / English 🇬🇧)**:
  - Instant one-click toggle with persisted language preferences in `Sessions/settings.json`.
- **Desktop Notifications & System Tray**:
  - Floating desktop toast notification window with direct click-to-activate.
  - Taskbar flashing via Win32 `FlashWindowEx`.
  - System Tray integration with minimize-to-tray, quick restore, and context menu.
  - Per-account mute toggle for uninterrupted focus.
- **Intelligent WebView2 Lifecycle & Memory Management**:
  - **Live RAM Monitor**: Real-time RAM widget tracking both WPF host and child `msedgewebview2.exe` renderer processes.
  - **⚡ Trim RAM Button**: Instant memory optimization leveraging Win32 `SetProcessWorkingSetSize` and forced generation-2 garbage collection.
  - **Tiered Background Inactivity Sleep**:
    - **Light Suspend (10 min idle)**: Freezes JavaScript timers and suspends DOM rendering (`TrySuspendAsync`), slashing RAM consumption by 60–80%.
    - **Deep Hibernation (30 min idle or manual)**: Completely disposes the `WebView2` control (0% RAM footprint) while preserving session state, cookies, and localStorage.
  - **Instant Lazy Wakeup**: Recreates and navigates hibernated instances on demand upon selection or through the context menu.
  - **Single-Instance Enforcement**: Named system mutex with Win32 broadcast restoration preventing duplicate resource consumption.
  - **Zoom Controls**: Quick zoom adjustments (`-`, `100%`, `+`) with keyboard shortcuts.

---

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + 1..9` | Switch directly to Account 1 through 9 |
| `Ctrl + F` | Focus search bar to filter accounts & groups |
| `Ctrl + +` / `Ctrl + -` | Zoom in / Zoom out active webview |
| `Ctrl + 0` | Reset zoom to 100% |
| `Esc` | Close profile creation modal or clear search |

---

## Architecture & Technology Stack

- **Target Framework**: .NET 8.0 Windows (`net8.0-windows`)
- **UI Framework**: Windows Presentation Foundation (WPF) with Slate/Indigo enterprise design tokens
- **Browser Engine**: Microsoft Edge WebView2 (`Microsoft.Web.WebView2` NuGet)
- **Interoperability**: Win32 native APIs (`Shell_NotifyIcon`, `FlashWindowEx`, `SetForegroundWindow`)
- **Distribution**: Framework-dependent single-file portable release (`MultiTeam.exe`, ~1.17 MB)

---

## Getting Started

### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (pre-installed on Windows 10/11)

### Build from Source
```powershell
# Restore and build debug
dotnet build

# Publish single-file release (portable ~1.17 MB)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```

### Running
Run `MultiTeam.exe` from the build or publish folder:
```powershell
.\publish\MultiTeam.exe
```

---

## License
MIT License.
