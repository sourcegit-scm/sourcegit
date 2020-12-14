cd src

dotnet publish -c Release -r win-x64 -o ..\publish\ -p:PublishSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true
Compress-Archive -Path ..\publish\SourceGit.exe -DestinationPath ..\publish\SourceGit.zip
del /f /s /q ..\publish\SourceGit.exe

dotnet publish -c Release -r win-x64 -o ..\publish\ -p:PublishSingleFile=true --self-contained=false