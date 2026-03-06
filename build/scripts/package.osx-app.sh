#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

cd build

mkdir -p SourceGit.app/Contents/Resources
mv SourceGit SourceGit.app/Contents/MacOS
cp resources/app/App.icns SourceGit.app/Contents/Resources/App.icns
sed "s/SOURCE_GIT_VERSION/$VERSION/g" resources/app/App.plist > SourceGit.app/Contents/Info.plist
rm -rf SourceGit.app/Contents/MacOS/SourceGit.dsym
rm -f SourceGit.app/Contents/MacOS/*.pdb

zip "sourcegit_$VERSION.$RUNTIME.zip" -r SourceGit.app
