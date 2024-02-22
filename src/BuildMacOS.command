#!/bin/sh

dotnet publish -c Release -r osx-x64 -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
dotnet publish -c Release -r osx-arm64 -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained

rm -rf build

mkdir -p build/SourceGit
mkdir -p build/SourceGit/x64/SourceGit.app/Contents/MacOS
mkdir -p build/SourceGit/arm64/SourceGit.app/Contents/MacOS
mkdir -p build/SourceGit/x64/SourceGit.app/Contents/Resources
mkdir -p build/SourceGit/arm64/SourceGit.app/Contents/Resources

cp App.plist build/SourceGit/x64/SourceGit.app/Contents/Info.plist
cp App.plist build/SourceGit/arm64/SourceGit.app/Contents/Info.plist

cp App.icns build/SourceGit/x64/SourceGit.app/Contents/Resources/App.icns
cp App.icns build/SourceGit/arm64/SourceGit.app/Contents/Resources/App.icns

cp -r bin/Release/net8.0/osx-x64/publish/* build/SourceGit/x64/SourceGit.app/Contents/MacOS/
cp -r bin/Release/net8.0/osx-arm64/publish/* build/SourceGit/arm64/SourceGit.app/Contents/MacOS/

rm -rf build/SourceGit/x64/SourceGit.app/Contents/MacOS/SourceGit.dsym
rm -rf build/SourceGit/arm64/SourceGit.app/Contents/MacOS/SourceGit.dsym

cd build
zip SourceGit.macOS.zip -r SourceGit