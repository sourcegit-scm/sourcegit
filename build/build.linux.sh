#!/bin/sh

version=`cat ../VERSION`

# Cleanup
rm -rf SourceGit *.tar.gz resources/deb/opt *.deb *.rpm *.AppImage

# Generic AppImage
cd resources/appimage
./publish-appimage -y -o sourcegit-${version}.linux.x86_64.AppImage

# Move to build dir
mv AppImages/sourcegit-${version}.linux.x86_64.AppImage ../../
mv AppImages/AppDir/usr/bin ../../SourceGit
cd ../../

# Debain/Ubuntu package
mkdir -p resources/deb/opt/sourcegit/
mkdir -p resources/deb/usr/bin
mkdir -p resources/deb/usr/share/applications
mkdir -p resources/deb/usr/share/icons
cp -f SourceGit/* resources/deb/opt/sourcegit/
ln -sf ../../opt/sourcegit/sourcegit resources/deb/usr/bin
cp -r resources/_common/applications resources/deb/usr/share/
cp -r resources/_common/icons resources/deb/usr/share/
sed -i "2s/.*/Version: ${version}/g" resources/deb/DEBIAN/control
dpkg-deb --root-owner-group --build resources/deb ./sourcegit_${version}-1_amd64.deb

# Redhat/CentOS/Fedora package
rpmbuild -bb --target=x86_64 resources/rpm/SPECS/build.spec --define "_topdir `pwd`/resources/rpm" --define "_version ${version}"
mv resources/rpm/RPMS/x86_64/sourcegit-${version}-1.x86_64.rpm .

rm -rf SourceGit
