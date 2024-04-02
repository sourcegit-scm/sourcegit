#!/bin/sh

rm -rf SourceGit.app

mkdir -p SourceGit.app/Contents/Resources
cp resources/App.plist SourceGit.app/Contents/Info.plist
cp resources/App.icns SourceGit.app/Contents/Resources/App.icns

mkdir -p SourceGit.app/Contents/MacOS
dotnet publish ../src/SourceGit.csproj -c Release -r osx-arm64 -o SourceGit.app/Contents/MacOS -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
zip SourceGit.osx-arm64.zip -r SourceGit.app -x "*/en/*" -x "*/zh/*" -x "*/*\.dsym/*"

rm -rf SourceGit.app/Contents/MacOS

mkdir -p SourceGit.app/Contents/MacOS
dotnet publish ../src/SourceGit.csproj -c Release -r osx-x64 -o SourceGit.app/Contents/MacOS -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
zip SourceGit.osx-x64.zip -r SourceGit.app -x "*/en/*" -x "*/zh/*" -x "*/*\.dsym/*"
