Remove-Item -Path build\DevBoard\*.pdb -Force
Compress-Archive -Path build\DevBoard -DestinationPath "build\devboard_${env:VERSION}.${env:RUNTIME}.zip" -Force