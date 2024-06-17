$version = Get-Content ..\VERSION

if (Test-Path SourceGit) {
    Remove-Item SourceGit -Recurse -Force
}

Remove-Item *.zip -Force

dotnet publish ..\src\SourceGit.csproj -c Release -r win-arm64 -o SourceGit -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained

Remove-Item SourceGit\*.pdb -Force

Compress-Archive -Path SourceGit -DestinationPath "sourcegit_$version.win-arm64.zip"

if (Test-Path SourceGit) {
    Remove-Item SourceGit -Recurse -Force
}

dotnet publish ..\src\SourceGit.csproj -c Release -r win-x64 -o SourceGit -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained

Remove-Item SourceGit\*.pdb -Force

Compress-Archive -Path SourceGit -DestinationPath "sourcegit_$version.win-x64.zip"
