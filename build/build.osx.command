#!/bin/sh

version=`cat ./VERSION`

rm -rf SourceGit.app *.zip

mkdir -p SourceGit.app/Contents/Resources
cp resources/app/App.plist SourceGit.app/Contents/Info.plist
cp resources/app/App.icns SourceGit.app/Contents/Resources/App.icns
sed -i "s/SOURCE_GIT_VERSION/${version}/g" SourceGit.app/Contents/Info.plist

mkdir -p SourceGit.app/Contents/MacOS
dotnet publish ../src/SourceGit.csproj -c Release -r osx-arm64 -o SourceGit.app/Contents/MacOS -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
zip sourcegit_${version}.osx-arm64.zip -r SourceGit.app -x "*/*\.dsym/*"

rm -rf SourceGit.app/Contents/MacOS

mkdir -p SourceGit.app/Contents/MacOS
dotnet publish ../src/SourceGit.csproj -c Release -r osx-x64 -o SourceGit.app/Contents/MacOS -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
zip sourcegit_${version}.osx-x64.zip -r SourceGit.app -x "*/*\.dsym/*"

rm -rf SourceGit.app