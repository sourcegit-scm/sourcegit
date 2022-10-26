@echo off

rmdir /s /q publish

cd src

rmdir /s /q bin
rmdir /s /q obj

dotnet publish SourceGit.csproj --nologo -c Release -r win-x86 -f net48 -o ..\publish\net48
ilrepack /ndebug /wildcards /out:..\publish\SourceGit.exe ..\publish\net48\SourceGit.exe ..\publish\net48\*.dll

cd ..\publish

rmdir /s /q net48

cd ..