# Change-view menu runtime regression

With .NET 10 and the app's native Skia dependencies installed:

```sh
dotnet run --project tests/ChangeViewModeMenu -c Release -p:DisableAOT=true
```

This harness loads the production app styles and `ChangeViewModeSwitcher` in
Avalonia Headless. It sends real pointer and keyboard input, checking the original
three parent icons, the sole nested compact-folders checkbox, parent selection,
hover and Right Arrow expansion, Enter/Space activation, Escape, repeated toggles,
and that checkbox activation never changes display mode. It restores the original
in-memory compact-folders preference. Use an isolated `XDG_CONFIG_HOME` on Linux
if needed. Set `SOURCEGIT_MENU_SCREENSHOT` to a PNG path for a rendered QA image.
