#!/bin/sh

version=`cat VERSION`

rm -rf SourceGit.app

mkdir -p SourceGit.app/Contents/Resources
mkdir -p SourceGit.app/Contents/MacOS
cp build/resources/app/App.icns SourceGit.app/Contents/Resources/App.icns
sed "s/SOURCE_GIT_VERSION/${version}/g" build/resources/app/App.plist > SourceGit.app/Contents/Info.plist
dotnet publish src/SourceGit.csproj -c Release -r osx-arm64 -o SourceGit.app/Contents/MacOS
rm -rf SourceGit.app/Contents/MacOS/SourceGit.dsym