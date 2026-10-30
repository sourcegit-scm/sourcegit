# Repository tab drag feedback fixture

Run from an initialized SourceGit checkout with .NET SDK 10 and Python 3:

```sh
python3 tests/TabDragFeedback/run.py
```

The runner builds the real SourceGit views and executes four headless rendering
cases: a long fixed-width title, a short title, a welcome tab, and a variable-width
tab. It checks effective source opacity and destination background, preview
clipping and ellipsis, icon selection, both insertion directions, leave/re-entry,
cancellation, drop ordering, and detach cleanup. Logs and PNG previews are saved
in a new temporary directory; `--output DIRECTORY` selects its parent.

Both XDG roots and their SourceGit subdirectories are created before application
initialization. The fixture verifies the resolved application directories and
requires exactly three initial pages. This prevents Linux's fallback to personal
configuration when an XDG root does not exist, including unintended workspace
restoration and scrolling. Each case receives fresh configuration and a private
copy of the user's existing fontconfig cache. A native font initialization crash
fails the test and is recorded in its log; the runner does not preload libraries
or retry failed cases.

To test the same fixture against an Avalonia 12 Wayland checkout:

```sh
python3 tests/TabDragFeedback/run.py \
  --source-project /path/to/wayland-checkout/src/SourceGit.csproj \
  --avalonia-version 12.1.4
```

`--dotnet /path/to/dotnet` selects an SDK outside `PATH`. The headless package
version must match that checkout's Avalonia version. The fixture invokes feedback
handlers directly; compositor input, native drag sessions, and backend selection
need separate native Wayland/X11 interaction checks.
