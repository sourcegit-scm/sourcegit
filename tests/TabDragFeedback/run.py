#!/usr/bin/env python3
"""Build and run the tab drag feedback fixture with isolated application state."""

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--source-project", type=Path)
    parser.add_argument("--avalonia-version", default="11.3.22")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    fixture = Path(__file__).resolve().parent
    output = (args.output or Path(tempfile.mkdtemp(prefix="sourcegit-tab-drag-"))).resolve()
    output.mkdir(parents=True, exist_ok=True)
    run = Path(tempfile.mkdtemp(prefix="run-", dir=output))
    env = os.environ.copy()
    # Native backend selection and portable mode must not select personal state.
    env.pop("WAYLAND_DISPLAY", None)
    env.pop("APPIMAGE", None)
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    project = fixture / "TabDragFeedback.csproj"
    properties = [f"-p:AvaloniaVersion={args.avalonia_version}", "-p:DisableAOT=true"]
    if args.source_project:
        properties.append(f"-p:SourceProject={args.source_project.resolve()}")
    build = [args.dotnet, "build", str(project), "-c", "Release",
             "-p:UseSharedCompilation=false", "-maxCpuCount:4", *properties]
    with (run / "build.log").open("w") as log:
        subprocess.run(build, cwd=fixture, env=env, stdout=log,
                       stderr=subprocess.STDOUT, check=True)

    # Preserve normal font discovery without writing to the user's font cache.
    # Cold font-cache/native HarfBuzz failures remain visible in the case logs.
    font_cache = Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache")) / "fontconfig"
    for case in ("long", "short", "welcome", "variable"):
        case_dir = run / case
        for relative in ("config/SourceGit", "cache/SourceGit"):
            (case_dir / relative).mkdir(parents=True)
        if font_cache.is_dir():
            shutil.copytree(font_cache, case_dir / "cache/fontconfig")
        case_env = env | {"TEST_OUTPUT": str(case_dir),
                          "XDG_CONFIG_HOME": str(case_dir / "config"),
                          "XDG_CACHE_HOME": str(case_dir / "cache")}
        command = [args.dotnet, str(fixture / "bin/Release/net10.0/TabDragFeedback.dll"), case]
        with (case_dir / "test.log").open("w") as log:
            result = subprocess.run(command, cwd=fixture, env=case_env,
                                    stdout=log, stderr=subprocess.STDOUT)
        if result.returncode:
            raise SystemExit(f"FAIL {case}: see {case_dir / 'test.log'}")
        print(f"PASS {case}: {case_dir}", flush=True)
    print(f"All four cases passed. Logs and rendered previews: {run}")


if __name__ == "__main__":
    main()
