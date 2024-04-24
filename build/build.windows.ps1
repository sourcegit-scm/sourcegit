$version = Get-Content ..\VERSION

if (Test-Path SourceGit) {
    Remove-Item SourceGit -Recurse -Force
}

Remove-Item *.zip -Force

# Add dependencies for build
dotnet add ..\src\SourceGit.csproj package Avalonia --version 11.0.10

dotnet publish ..\src\SourceGit.csproj -c Release -r win-x64 -o SourceGit -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained

Remove-Item SourceGit\*.pdb -Force

Compress-Archive -Path SourceGit -DestinationPath "sourcegit_$version.win-x64.zip"
