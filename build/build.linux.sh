#!/bin/sh

version=`cat ../VERSION`

# Cleanup
rm -rf SourceGit *.tar.gz resources/deb/opt *.deb *.rpm

# Compile
dotnet publish ../src/SourceGit.csproj -c Release -r linux-x64 -o SourceGit -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=link --self-contained
mv SourceGit/SourceGit SourceGit/sourcegit
cp resources/app/App.icns SourceGit/sourcegit.icns
rm -f SourceGit/*.dbg

# General Linux archive
tar -zcvf sourcegit_${version}.linux-x64.tar.gz SourceGit
rm -f SourceGit/sourcegit.icns

# Debain/Ubuntu package
mkdir -p resources/deb/opt/sourcegit/
mkdir -p resources/deb/usr/share/applications
mkdir -p resources/deb/usr/share/icons
cp -f SourceGit/* resources/deb/opt/sourcegit/
cp -r resources/_common/applications resources/deb/usr/share/
cp -r resources/_common/icons resources/deb/usr/share/
chmod +x -R resources/deb/opt/sourcegit
sed -i "2s/.*/Version: ${version}/g" resources/deb/DEBIAN/control
dpkg-deb --build resources/deb ./sourcegit_${version}-1_amd64.deb

# Redhat/CentOS/Fedora package
rpmbuild -bb --target=x86_64 resources/rpm/SPECS/build.spec --define "_topdir `pwd`/resources/rpm" --define "_version ${version}"
mv resources/rpm/RPMS/x86_64/sourcegit-${version}-1.x86_64.rpm .

rm -rf SourceGit
