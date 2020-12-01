@echo off

cd src

dotnet publish -c Release -r win-x64 -o ..\publish\selfcontained\ -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained=true
dotnet publish -c Release -r win-x64 -o ..\publish\no-selfcontained\ -p:PublishSingleFile=true --self-contained=false

pause