@echo off

rmdir /s /q publish

cd src
dotnet publish SourceGit.csproj --nologo -c Release -r win-x64 -f net6.0-windows -p:PublishSingleFile=true --no-self-contained -o ..\publish
dotnet publish SourceGit.csproj --nologo -c Release -r win-x64 -f net6.0-windows --self-contained -o ..\publish\SourceGit

cd ..