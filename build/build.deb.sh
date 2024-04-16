#!/bin/sh

rm -rf SourceGit resources/deb/opt *.deb
dotnet publish ../src/SourceGit.csproj -c Release -r linux-x64 -o SourceGit -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained

mkdir -p resources/deb/opt/sourcegit
mv SourceGit/SourceGit resources/deb/opt/sourcegit/sourcegit
mv SourceGit/*.so resources/deb/opt/sourcegit/

chmod -R 755 resources/deb

rm -rf SourceGit

sudo dpkg-deb --build resources/deb ./sourcegit_8.8_amd64.deb
