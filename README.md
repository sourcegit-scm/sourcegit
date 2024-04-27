# SourceGit

Opensource Git GUI client.

## Highlights

* Supports Windows/macOS/Linux
* Opensource/Free
* Fast
* English/简体中文
* Built-in light/dark themes
* Visual commit graph
* Supports SSH access with each remote
* GIT commands with GUI
  * Clone/Fetch/Pull/Push...
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
* GitFlow support

> **Linux** only tested on **Ubuntu 22.04** on **X11**.

## How to Use

**To use this tool, you need to install Git(>=2.23.0) first.**

You can download the latest stable from [Releases](https://github.com/sourcegit-scm/sourcegit/releases/latest) or download workflow artifacts from [Github Actions](https://github.com/sourcegit-scm/sourcegit/actions) to try this app based on latest commits.

This software creates a folder `$"{System.Environment.SpecialFolder.ApplicationData}/SourceGit"`, which is platform-dependent, to store user settings, downloaded avatars and crash logs. 

| OS | PATH |
| --- | --- |
| Windows | `C:\Users\USER_NAME\AppData\Roaming\SourceGit` |
| Linux | `/home/USER_NAME/.config/SourceGit` |
| macOS | `/Users/USER_NAME/.config/SourceGit` |

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
| JetBrains Fleet | YES | YES | YES | FLEET_PATH |
| Sublime Text | YES | YES | YES | SUBLIME_TEXT_PATH |

> You can set the given environment variable for special tool if it can NOT be found by this app automatically. 

## Screenshots

* Dark Theme

![Theme Dark](./screenshots/theme_dark.png)

* Light Theme

![Theme Light](./screenshots/theme_light.png)

## Contributing

Thanks to all the people who contribute.

<a href="https://github.com/sourcegit-scm/sourcegit/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=sourcegit-scm/sourcegit&t=2" />
</a>
