#!/bin/sh

dotnet publish -c Release -r osx-x64 -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
dotnet publish -c Release -r osx-arm64 -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained

rm -rf macOS
mkdir -p macOS
mkdir -p macOS/x64/SourceGit.app/Contents/MacOS
mkdir -p macOS/arm64/SourceGit.app/Contents/MacOS
mkdir -p macOS/x64/SourceGit.app/Contents/Resources
mkdir -p macOS/arm64/SourceGit.app/Contents/Resources

cp App.plist macOS/x64/SourceGit.app/Contents/Info.plist
cp App.plist macOS/arm64/SourceGit.app/Contents/Info.plist

cp App.icns macOS/x64/SourceGit.app/Contents/Resources/App.icns
cp App.icns macOS/arm64/SourceGit.app/Contents/Resources/App.icns

cp -r bin/Release/net8.0/osx-x64/publish/* macOS/x64/SourceGit.app/Contents/MacOS/
cp -r bin/Release/net8.0/osx-arm64/publish/* macOS/arm64/SourceGit.app/Contents/MacOS/

rm -rf macOS/x64/SourceGit.app/Contents/MacOS/SourceGit.dsym
rm -rf macOS/arm64/SourceGit.app/Contents/MacOS/SourceGit.dsym
