# SourceGit - Opensource Git GUI client.

[![stars](https://img.shields.io/github/stars/sourcegit-scm/sourcegit.svg)](https://github.com/sourcegit-scm/sourcegit/stargazers)
[![forks](https://img.shields.io/github/forks/sourcegit-scm/sourcegit.svg)](https://github.com/sourcegit-scm/sourcegit/forks)
[![license](https://img.shields.io/github/license/sourcegit-scm/sourcegit.svg)](LICENSE)
[![latest](https://img.shields.io/github/v/release/sourcegit-scm/sourcegit.svg)](https://github.com/sourcegit-scm/sourcegit/releases/latest)
[![downloads](https://img.shields.io/github/downloads/sourcegit-scm/sourcegit/total)](https://github.com/sourcegit-scm/sourcegit/releases)

## Highlights

* Supports Windows/macOS/Linux
* Opensource/Free
* Fast
* Deutsch/English/Español/Français/Português/Русский/简体中文/繁體中文
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

[![en_US](https://img.shields.io/badge/en__US-100%25-brightgreen)](TRANSLATION.md) [![de__DE](https://img.shields.io/badge/de__DE-99.86%25-yellow)](TRANSLATION.md) [![es__ES](https://img.shields.io/badge/es__ES-98.01%25-yellow)](TRANSLATION.md) [![fr__FR](https://img.shields.io/badge/fr__FR-97.44%25-yellow)](TRANSLATION.md) [![pt__BR](https://img.shields.io/badge/pt__BR-99.29%25-yellow)](TRANSLATION.md) [![ru__RU](https://img.shields.io/badge/ru__RU-100.00%25-brightgreen)](TRANSLATION.md) [![zh__CN](https://img.shields.io/badge/zh__CN-100.00%25-brightgreen)](TRANSLATION.md) [![zh__TW](https://img.shields.io/badge/zh__TW-100.00%25-brightgreen)](TRANSLATION.md)

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

* Thanks [@ybeapps](https://github.com/ybeapps) for making `SourceGit` available on `Homebrew`. You can simply install it with following command:
  ```shell
  brew tap ybeapps/homebrew-sourcegit
  brew install --cask --no-quarantine sourcegit
  ```
* If you want to install `SourceGit.app` from Github Release manually, you need run following command to make sure it works:
  ```shell
  sudo xattr -cr /Applications/SourceGit.app
  ```
* Make sure [git-credential-manager](https://github.com/git-ecosystem/git-credential-manager/releases) is installed on your mac.
* You can run `echo $PATH > ~/Library/Application\ Support/SourceGit/PATH` to generate a custom PATH env file to introduce `PATH` env to SourceGit.

For **Linux** users:

* `xdg-open` must be installed to support open native file manager.
* Make sure [git-credential-manager](https://github.com/git-ecosystem/git-credential-manager/releases) is installed on your linux.
* Maybe you need to set environment variable `AVALONIA_SCREEN_SCALE_FACTORS`. See https://github.com/AvaloniaUI/Avalonia/wiki/Configuring-X11-per-monitor-DPI.
* If you can NOT type accented characters, such as `ê`, `ó`, try to set the environment variable `AVALONIA_IM_MODULE` to `none`. 

## OpenAI

This software supports using OpenAI or other AI service that has an OpenAI comaptible HTTP API to generate commit message. You need configurate the service in `Preference` window.

For `OpenAI`:

* `Server` must be `https://api.openai.com/v1/chat/completions`

For other AI service:

* The `Server` should fill in a URL equivalent to OpenAI's `https://api.openai.com/v1/chat/completions`. For example, when using `Ollama`, it should be `http://localhost:11434/v1/chat/completions` instead of `http://localhost:11434/api/generate`
* The `API Key` is optional that depends on the service

## External Tools

This app supports open repository in external tools listed in the table below.

| Tool                          | Windows | macOS | Linux |
|-------------------------------|---------|-------|-------|
| Visual Studio Code            | YES     | YES   | YES   |
| Visual Studio Code - Insiders | YES     | YES   | YES   |
| VSCodium                      | YES     | YES   | YES   |
| Fleet                         | YES     | YES   | YES   |
| Sublime Text                  | YES     | YES   | YES   |
| Zed                           | NO      | YES   | YES   |
| Visual Studio                 | YES     | NO    | NO    |

> [!NOTE]
> This app will try to find those tools based on some pre-defined or expected locations automatically. If you are using one portable version of these tools, it will not be detected by this app.
> To solve this problem you can add a file named `external_editors.json` in app data dir and provide the path directly. For example:
```json
{
    "tools": {
        "Visual Studio Code": "D:\\VSCode\\Code.exe"
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

[![Contributors](https://contrib.rocks/image?repo=sourcegit-scm/sourcegit&columns=20)](https://github.com/sourcegit-scm/sourcegit/graphs/contributors)
