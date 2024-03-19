#!/bin/sh

rm -rf SourceGit
dotnet publish ../src/SourceGit.csproj -c Release -r linux-x64 -o SourceGit -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
tar -zcvf SourceGit.linux-x64.tar.gz SourceGit --exclude=en --exclude=zh --exclude="*.dbg"
rm -rf SourceGit
