#!/bin/bash

set -e

if [ -z "$VERSION" ]; then
    echo "Provide the version as environment variable VERSION"
    exit 1
fi

if [ -z "$RUNTIME" ]; then
    echo "Provide the runtime as environment variable RUNTIME"
    exit 1
fi

cd build

mkdir -p SourceGit.app/Contents/Resources
mv SourceGit SourceGit.app/Contents/MacOS
cp resources/app/App.icns SourceGit.app/Contents/Resources/App.icns
sed "s/SOURCE_GIT_VERSION/$VERSION/g" resources/app/App.plist > SourceGit.app/Contents/Info.plist

zip "sourcegit_$VERSION.$RUNTIME.zip" -r SourceGit.app -x "*/*\.dsym/*"
