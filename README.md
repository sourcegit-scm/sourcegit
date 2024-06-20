# SourceGit

Opensource Git GUI client.

## Highlights

* Supports Windows/macOS/Linux
* Opensource/Free
* Fast
* English/简体中文/繁體中文
* Built-in light/dark themes
* Customize theme
* Visual commit graph
* Supports SSH access with each remote
* GIT commands with GUI
  * Clone/Fetch/Pull/Push...
  * Merge/Rebase/Reset/Revert/Amend/Cherry-pick...
  * Interactive rebase (Basic)
  * Branches
  * Remotes
  * Tags
  * Stashes
  * Submodules
  * Archive
  * Diff
  * Save as patch/apply
  * File histories
  * Blame
  * Revision Diffs
  * Branch Diff
  * Image Diff
* GitFlow support
* Git LFS support

> **Linux** only tested on **Debian 12** on both **X11** & **Wayland**.

## How to Use

**To use this tool, you need to install Git(>=2.23.0) first.**

You can download the latest stable from [Releases](https://github.com/sourcegit-scm/sourcegit/releases/latest) or download workflow artifacts from [Github Actions](https://github.com/sourcegit-scm/sourcegit/actions) to try this app based on latest commits.

This software creates a folder `$"{System.Environment.SpecialFolder.ApplicationData}/SourceGit"`, which is platform-dependent, to store user settings, downloaded avatars and crash logs. 

| OS | PATH |
| --- | --- |
| Windows | `C:\Users\USER_NAME\AppData\Roaming\SourceGit` |
| Linux | `${HOME}/.config/SourceGit` |
| macOS | `${HOME}/Library/Application Support/SourceGit` |

For **Windows** users:

* **MSYS Git is NOT supported**. Please use official [Git for Windows](https://git-scm.com/download/win) instead.
* `sourcegit_x.y.win-x64.zip` may be reported as virus by Windows Defender. I don't know why. I have manually tested the zip to be uploaded using Windows Defender before uploading and no virus was found. If you have installed .NET 8 SDK locally, I suggest you to compile it yourself. And if you have any idea about how to fix this, please open an issue.

For **macOS** users:

* Download `sourcegit_x.y.osx-x64.zip` or `sourcegit_x.y.osx-arm64.zip` from Releases. `x64` for Intel and `arm64` for Apple Silicon.
* Move `SourceGit.app` to `Applications` folder.
* Make sure your mac trusts all software from anywhere. For more information, search `spctl --master-disable`.
* Make sure [git-credential-manager](https://github.com/git-ecosystem/git-credential-manager/releases) is installed on your mac.
* You may need to run `sudo xattr -cr /Applications/SourceGit.app` to make sure the software works.

For **Linux** users:

* `xdg-open` must be installed to support open native file manager.
* Make sure [git-credential-manager](https://github.com/git-ecosystem/git-credential-manager/releases) is installed on your linux.
* Maybe you need to set environment variable `AVALONIA_SCREEN_SCALE_FACTORS`. See https://github.com/AvaloniaUI/Avalonia/wiki/Configuring-X11-per-monitor-DPI. 

## External Tools

This app supports open repository in external tools listed in the table below.

| Tool | Windows | macOS | Linux | Environment Variable |
| --- | --- | --- | --- | --- |
| Visual Studio Code | YES | YES | YES | VSCODE_PATH |
| Visual Studio Code - Insiders | YES | YES | YES | VSCODE_INSIDERS_PATH |
| VSCodium | YES | YES | YES | VSCODIUM_PATH |
| JetBrains Fleet | YES | YES | YES | FLEET_PATH |
| Sublime Text | YES | YES | YES | SUBLIME_TEXT_PATH |

> * You can set the given environment variable for special tool if it can NOT be found by this app automatically. 
> * Installing `JetBrains Toolbox` will help this app to find other JetBrains tools installed on your device.
> * On macOS, you may need to use `launchctl setenv` to make sure the app can read these environment variables.

## Screenshots

* Dark Theme

![Theme Dark](./screenshots/theme_dark.png)

* Light Theme

![Theme Light](./screenshots/theme_light.png)

## How to Customize Theme

1. Create a new json file, and provide your favorite colors with follow keys:

| Key | Description |
| --- | --- |
| Color.Window | Window background color |
| Color.WindowBorder | Window border color. Only used on Linux. |
| Color.TitleBar | Title bar background color |
| Color.ToolBar | Tool bar background color |
| Color.Popup | Popup panel background color |
| Color.Contents | Background color used in inputs, data grids, file content viewer, change lists, text diff viewer, etc. |
| Color.Badage | Badage background color |
| Color.Conflict | Conflict panel background color |
| Color.ConflictForeground | Conflict panel foreground color |
| Color.Border0 | Border color used in some controls, like Window, Tab, Toolbar, etc. |
| Color.Border1 | Border color used in inputs, like TextBox, ComboBox, etc. |
| Color.Border2 | Border color used in visual lines, like seperators, Rectange, etc. |
| Color.FlatButton.Background | Flat button background color, like `Cancel`, `Commit & Push` button |
| Color.FlatButton.BackgroundHovered | Flat button background color when hovered, like `Cancel` button |
| Color.FG1 | Primary foreground color for all text elements |
| Color.FG2 | Secondary foreground color for all text elements |
| Color.Diff.EmptyBG | Background color used in empty lines in diff viewer |
| Color.Diff.AddedBG | Background color used in added lines in diff viewer |
| Color.Diff.DeletedBG | Background color used in deleted lines in diff viewer |
| Color.Diff.AddedHighlight | Background color used for changed words in added lines in diff viewer |
| Color.Diff.DeletedHighlight | Background color used for changed words in deleted lines in diff viewer |

For example:

```json
{
  "Color.Window": "#FFFF6059"
}
```

2. Open `Preference` -> `Appearance`, choose the json file you just created in `Custom Color Schema`.

> **NOTE**: The `Custom Color Schema` will override the colors with same keys in current active theme.

## Contributing

Thanks to all the people who contribute.

[![Contributors](https://contrib.rocks/image?repo=sourcegit-scm/sourcegit&columns=10)](https://github.com/sourcegit-scm/sourcegit/graphs/contributors)
