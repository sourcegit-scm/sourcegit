rmdir /s /q publish

cd src
rmdir /s /q bin
rmdir /s /q obj
dotnet publish SourceGit.csproj --nologo -c Release -r win-x64 -p:PublishSingleFile=true --no-self-contained -o ../publish

rmdir /s /q bin
rmdir /s /q obj
dotnet publish SourceGit_48.csproj --nologo -c Release -r win-x64 -o ../publish/net48

cd ../publish
ilrepack /ndebug /out:SourceGit_48.exe net48/SourceGit.exe net48/Newtonsoft.Json.dll
rmdir /s /q net48

cd ../