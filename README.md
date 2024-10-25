# SourceGit - Opensource Git GUI client.

![stars](https://img.shields.io/github/stars/sourcegit-scm/sourcegit.svg) ![forks](https://img.shields.io/github/forks/sourcegit-scm/sourcegit.svg) ![license](https://img.shields.io/github/license/sourcegit-scm/sourcegit.svg) ![latest](https://img.shields.io/github/v/release/sourcegit-scm/sourcegit.svg) ![downloads](https://img.shields.io/github/downloads/sourcegit-scm/sourcegit/total)

## Highlights

* Supports Windows/macOS/Linux
* Opensource/Free
* Fast
* English/Français/Deutsch/Português/Русский/简体中文/繁體中文
* Built-in light/dark themes
* Customize theme
* Visual commit graph
* Supports SSH access with each remote
* GIT commands with GUI
  * Clone/Fetch/Pull/Push...
  * Merge/Rebase/Reset/Revert/Amend/Cherry-pick...
  * Amend/Reword
  * Interactive rebase (Basic)
  * Branches
  * Remotes
  * Tags
  * Stashes
  * Submodules
  * Worktrees
  * Archive
  * Diff
  * Save as patch/apply
  * File histories
  * Blame
  * Revision Diffs
  * Branch Diff
  * Image Diff - Side-By-Side/Swipe/Blend
* Search commits
* GitFlow
* Git LFS
* Issue Link
* Workspace
* Using AI to generate commit message (C# port of [anjerodev/commitollama](https://github.com/anjerodev/commitollama))

> [!WARNING]
> **Linux** only tested on **Debian 12** on both **X11** & **Wayland**.

## Translation Status

[![en_US](https://img.shields.io/badge/en__US-100%25-brightgreen)](TRANSLATION.md) [![de__DE](https://img.shields.io/badge/de__DE-98.95%25-yellow)](TRANSLATION.md) [![fr__FR](https://img.shields.io/badge/fr__FR-90.36%25-yellow)](TRANSLATION.md) [![pt__BR](https://img.shields.io/badge/pt__BR-93.52%25-yellow)](TRANSLATION.md) [![ru__RU](https://img.shields.io/badge/ru__RU-98.80%25-yellow)](TRANSLATION.md) [![zh__CN](https://img.shields.io/badge/zh__CN-99.10%25-yellow)](TRANSLATION.md) [![zh__TW](https://img.shields.io/badge/zh__TW-99.70%25-yellow)](TRANSLATION.md)

## How to Use

**To use this tool, you need to install Git(>=2.23.0) first.**

You can download the latest stable from [Releases](https://github.com/sourcegit-scm/sourcegit/releases/latest) or download workflow artifacts from [Github Actions](https://github.com/sourcegit-scm/sourcegit/actions) to try this app based on latest commits.

This software creates a folder `$"{System.Environment.SpecialFolder.ApplicationData}/SourceGit"`, which is platform-dependent, to store user settings, downloaded avatars and crash logs.

| OS      | PATH                                                |
|---------|-----------------------------------------------------|
| Windows | `C:\Users\USER_NAME\AppData\Roaming\SourceGit`      |
| Linux   | `${HOME}/.config/SourceGit` or `${HOME}/.sourcegit` |
| macOS   | `${HOME}/Library/Application Support/SourceGit`     |

> [!TIP]
> You can open the app data dir from the main menu.

For **Windows** users:

* **MSYS Git is NOT supported**. Please use official [Git for Windows](https://git-scm.com/download/win) instead.
* You can install the latest stable from `winget` with follow commands:
  ```shell
  winget install SourceGit
  ```
> [!NOTE]
> `winget` will install this software as a commandline tool. You need run `SourceGit` from console or `Win+R` at the first time. Then you can add it to the taskbar.
* You can install the latest stable by `scoope` with follow commands:
  ```shell
  scoop bucket add extras
  scoop install sourcegit
  ```
* Portable versions can be found in [Releases](https://github.com/sourcegit-scm/sourcegit/releases/latest)

For **macOS** users:

* Download `sourcegit_x.y.osx-x64.zip` or `sourcegit_x.y.osx-arm64.zip` from Releases. `x64` for Intel and `arm64` for Apple Silicon.
* Move `SourceGit.app` to `Applications` folder.
* Make sure your mac trusts all software from anywhere. For more information, search `spctl --master-disable`.
* Make sure [git-credential-manager](https://github.com/git-ecosystem/git-credential-manager/releases) is installed on your mac.
* You may need to run `sudo xattr -cr /Applications/SourceGit.app` to make sure the software works.
* You can run `echo $PATH > ~/Library/Application\ Support/SourceGit/PATH` to generate a custom PATH env file to introduce `PATH` env to SourceGit.

For **Linux** users:

* `xdg-open` must be installed to support open native file manager.
* Make sure [git-credential-manager](https://github.com/git-ecosystem/git-credential-manager/releases) is installed on your linux.
* Maybe you need to set environment variable `AVALONIA_SCREEN_SCALE_FACTORS`. See https://github.com/AvaloniaUI/Avalonia/wiki/Configuring-X11-per-monitor-DPI.

## OpenAI

This software supports using OpenAI or other AI service that has an OpenAI comaptible HTTP API to generate commit message. You need configurate the service in `Preference` window.

For `OpenAI`:

* `Server` must be `https://api.openai.com/v1/chat/completions`

For other AI service:

* The `Server` should fill in a URL equivalent to OpenAI's `https://api.openai.com/v1/chat/completions`. For example, when using `Ollama`, it should be `http://localhost:11434/v1/chat/completions` instead of `http://localhost:11434/api/generate`
* The `API Key` is optional that depends on the service

## External Tools

This app supports open repository in external tools listed in the table below.

| Tool                          | Windows | macOS | Linux | KEY IN `external_editors.json` |
|-------------------------------|---------|-------|-------|--------------------------------|
| Visual Studio Code            | YES     | YES   | YES   | VSCODE                         |
| Visual Studio Code - Insiders | YES     | YES   | YES   | VSCODE_INSIDERS                |
| VSCodium                      | YES     | YES   | YES   | VSCODIUM                       |
| JetBrains Fleet               | YES     | YES   | YES   | FLEET                          |
| Sublime Text                  | YES     | YES   | YES   | SUBLIME_TEXT                   |
| Zed                           | NO      | YES   | YES   | ZED                            |

> [!NOTE]
> This app will try to find those tools based on some pre-defined or expected locations automatically. If you are using one portable version of these tools, it will not be detected by this app.
> To solve this problem you can add a file named `external_editors.json` in app data dir and provide the path directly. For example:
```json
{
    "tools": {
        "VSCODE": "D:\\VSCode\\Code.exe"
    }
}
```

> [!NOTE]
> This app also supports a lot of `JetBrains` IDEs, installing `JetBrains Toolbox` will help this app to find them.

## Screenshots

* Dark Theme

  ![Theme Dark](./screenshots/theme_dark.png)

* Light Theme

  ![Theme Light](./screenshots/theme_light.png)

* Custom

  You can find custom themes from [sourcegit-theme](https://github.com/sourcegit-scm/sourcegit-theme.git). And welcome to share your own themes.

## Contributing

Everyone is welcome to submit a PR. Please make sure your PR is based on the latest `develop` branch and the target branch of PR is `develop`.

Thanks to all the people who contribute.

[![Contributors](https://contrib.rocks/image?repo=sourcegit-scm/sourcegit&columns=10)](https://github.com/sourcegit-scm/sourcegit/graphs/contributors)
