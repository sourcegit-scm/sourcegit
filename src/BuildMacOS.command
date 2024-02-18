#!/bin/sh

dotnet publish -c Release -r osx-x64 -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained