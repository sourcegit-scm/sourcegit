@echo off

cd src

dotnet publish -c Release -r win-x64 -o ..\publish\SourceGit\ -p:PublishSingleFile=true --self-contained=true
dotnet publish -c Release -r win-x64 -o ..\publish\ -p:PublishSingleFile=true --self-contained=false

pause