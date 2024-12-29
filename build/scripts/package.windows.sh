#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

cd build

rm -rf SourceGit/*.pdb

zip "sourcegit_$VERSION.$RUNTIME.zip" -r SourceGit
