#!/bin/sh

rm -rf SourceGit SourceGit.linux-x64.tar.gz
dotnet publish ../src/SourceGit.csproj -c Release -r linux-x64 -o SourceGit -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
tar -zcvf SourceGit.linux-x64.tar.gz --exclude="*/*.dbg" SourceGit
rm -rf SourceGit
