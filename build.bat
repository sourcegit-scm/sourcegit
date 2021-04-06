cd src
rmdir /s /q bin
rmdir /s /q obj
dotnet publish --nologo -c Release -r win-x64 -p:PublishSingleFile=true --no-self-contained -o ../publish

rmdir /s /q bin
rmdir /s /q obj
dotnet publish --nologo -c Release -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained -o ../publish/SourceGit
cd ..