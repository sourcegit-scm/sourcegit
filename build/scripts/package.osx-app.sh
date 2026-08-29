#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

cd build

mkdir -p DevBoard.app/Contents/Resources
mv DevBoard DevBoard.app/Contents/MacOS
cp resources/app/App.icns DevBoard.app/Contents/Resources/App.icns
sed "s/SOURCE_GIT_VERSION/$VERSION/g" resources/app/App.plist > DevBoard.app/Contents/Info.plist
rm -rf DevBoard.app/Contents/MacOS/DevBoard.dsym
rm -f DevBoard.app/Contents/MacOS/*.pdb

zip "devboard_$VERSION.$RUNTIME.zip" -r DevBoard.app
