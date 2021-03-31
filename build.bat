cd src
rmdir /s /q bin
rmdir /s /q obj
dotnet publish --nologo -c Release -r win-x64 -p:PublishSingleFile=true --no-self-contained -o publish
cd ..