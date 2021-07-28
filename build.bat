@echo off

rmdir /s /q publish

cd src
rmdir /s /q bin
rmdir /s /q obj
dotnet publish SourceGit_48.csproj --nologo -c Release -r win-x86 -o ..\publish\net48
ilrepack /ndebug /out:..\publish\SourceGit.exe ..\publish\net48\SourceGit.exe ..\publish\net48\Newtonsoft.Json.dll ..\publish\net48\DiffPlex.dll
cd ..\publish
ren SourceGit.exe SourceGit_48.exe
rmdir /s /q net48

cd ..\src
rmdir /s /q bin
rmdir /s /q obj
dotnet publish SourceGit.csproj --nologo -c Release -r win-x86 -p:PublishSingleFile=true --no-self-contained -o ..\publish

cd ../