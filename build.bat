cd src
rmdir /s /q bin
rmdir /s /q obj
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
cd ..