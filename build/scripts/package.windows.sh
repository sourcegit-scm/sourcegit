#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

cd build

rm -rf SourceGit/*.pdb

if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" || "$OSTYPE" == "win32" ]]; then
  powershell -Command "Compress-Archive -Path SourceGit\\* -DestinationPath \"sourcegit_$VERSION.$RUNTIME.zip\" -Force"
else
  zip "sourcegit_$VERSION.$RUNTIME.zip" -r SourceGit
fi
