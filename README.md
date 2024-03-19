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

## How to use

**To use this tool, you need to install Git first.**

You can download the latest stable from [Releases](https://github.com/sourcegit-scm/sourcegit/releases/latest) or download workflow artifacts from [Github Actions](https://github.com/sourcegit-scm/sourcegit/actions) to try this app based on each commits.


For **macOS** users:

* Download `SourceGit.osx-x64.zip` or `SourceGit.osx-arm64.zip` from Releases. `x64` for Intel and `arm64` for Apple Silicon.
* Move `SourceGit.app` to `Applications` folder.
* Make sure your mac trusts all software from anywhere. For more information, search `spctl --master-disable`.
* You may need to run `sudo xattr -cr /Applications/SourceGit.app` to make sure the software works.

For **Linux** users:

* `xdg-open` must be installed to support open native file manager.
* Maybe you need to set environment variable `AVALONIA_SCREEN_SCALE_FACTORS`. See https://github.com/AvaloniaUI/Avalonia/wiki/Configuring-X11-per-monitor-DPI. 

## Screen Shots

* Dark Theme

![Theme Dark](./screenshots/theme_dark.png)

* Light Theme

![Theme Light](./screenshots/theme_light.png)

## Contributing

Thanks to all the people who contribute.

<a href="https://github.com/sourcegit-scm/sourcegit/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=sourcegit-scm/sourcegit" />
</a>
