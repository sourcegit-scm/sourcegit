if (Test-Path SourceGit) {
    Remove-Item SourceGit -Recurse -Force
}

if (Test-Path SourceGit.win-x64.zip) {
    Remove-Item SourceGit.win-x64.zip -Force
}

dotnet publish ..\src\SourceGit.csproj -c Release -r win-x64 -o SourceGit -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained

Remove-Item SourceGit\*.pdb -Force
Remove-Item SourceGit\zh -Recurse -Force
Remove-Item SourceGit\en -Recurse -Force

Compress-Archive -Path SourceGit -DestinationPath SourceGit.win-x64.zip
