# SourceGit

Opensouce Git GUI client.

## High-lights

* Supports Windows/macOS/Linux
* Opensource/Free
* Fast
* English/简体中文
* Build-in light/dark themes
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

## How to use

**To use this tool, you need to install Git first.**

You can download the latest stable from [Releases](https://github.com/sourcegit-scm/sourcegit/releases/latest) or download workflow artifacts from [Github Actions](https://github.com/sourcegit-scm/sourcegit/actions) to try this app based on each commits.


For **macOS** users:

* Download `SourceGit.macOS.zip` from Releases.
* Choose the app that fits your system's CPU architecture and copy it to Applications. `x64` for Intel and `arm64` for Apple Silicon.
* Make sure your mac trusts all software from anywhere. For more information, search `spctl --master-disable`.
* You may need to run `sudo xattr -cr /Applications/SourceGit.app` to make sure the software works.

For **Linux** users:

* `xdg-open` must be installed to support open native file manager.
* Only tested on `Ubuntu 22.04`.

## Screen Shots

* Drak Theme

![Theme Dark](./screenshots/theme_dark.png)

* Light Theme

![Theme Light](./screenshots/theme_light.png)

## Thanks

* [gigi81](https://github.com/gigi81) Github actions integration
* [kekekeks](https://github.com/kekekeks) Way to stage/unstage/discard selected changes in a file.
* [XiaoLinger](https://gitee.com/LingerNN) Hotkey: `CTRL + Enter` to commit
* [carterl](https://gitee.com/carterl) Supports Windows Terminal; Rewrite way to find git executable
* [PUMA](https://gitee.com/whgfu) Configure for default user
* [Rwing](https://gitee.com/rwing) GitFlow: add an option to keep branch after finish
* [XiaoLinger](https://gitee.com/LingerNN) Fix localizations in popup panel
