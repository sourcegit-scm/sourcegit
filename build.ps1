cd src

dotnet publish -c Release -r win-x64 -o ..\publish\ -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=link -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true
Compress-Archive -Update -Path ..\publish\SourceGit.exe -DestinationPath ..\publish\SourceGit.zip

dotnet publish -c Release -r win-x64 -o ..\publish\ -p:PublishSingleFile=true --self-contained=false