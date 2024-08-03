#!/bin/bash

set -e

if [ -z "$VERSION" ]; then
    echo "Provide the version as environment variable VERSION"
    exit 1
fi

if [ -z "$RUNTIME" ]; then
    echo "Provide the runtime as environment variable RUNTIME"
    exit 1
fi

arch=
appimage_arch=
appimage_runtime=
target=
case "$RUNTIME" in
    linux-x64)
        arch=amd64
        appimage_arch=x86_64
        appimage_runtime=runtime-fuse2-x86_64
        target=x86_64;;
    linux-arm64)
        arch=arm64
        appimage_arch=arm_aarch64
        appimage_runtime=runtime-fuse2-aarch64
        target=aarch64;;
    *)
        echo "Unknown runtime $RUNTIME"
        exit 1;;
esac

APPIMAGETOOL_URL=https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
APPIMAGE_RUNTIME_URL=https://github.com/AppImage/type2-runtime/releases/download/old/$appimage_runtime 

cd build

if [ ! -f "appimagetool" ]; then
    curl -o appimagetool -L "$APPIMAGETOOL_URL"
    chmod +x appimagetool
fi

if [ ! -f "appimage_runtime" ]; then
    curl -o appimage_runtime -L "$APPIMAGE_RUNTIME_URL"
fi

rm -f SourceGit/*.dbg

mkdir -p SourceGit.AppDir/opt
mkdir -p SourceGit.AppDir/usr/share/metainfo
mkdir -p SourceGit.AppDir/usr/share/applications

cp -r SourceGit SourceGit.AppDir/opt/sourcegit
desktop-file-install resources/_common/applications/sourcegit.desktop --dir SourceGit.AppDir/usr/share/applications \
    --set-icon com.sourcegit_scm.SourceGit --set-key=Exec --set-value=AppRun
mv SourceGit.AppDir/usr/share/applications/{sourcegit,com.sourcegit_scm.SourceGit}.desktop
cp resources/appimage/sourcegit.png SourceGit.AppDir/com.sourcegit_scm.SourceGit.png
ln -sf /opt/sourcegit/sourcegit SourceGit.AppDir/AppRun
ln -rsf SourceGit.AppDir/usr/share/applications/com.sourcegit_scm.SourceGit.desktop SourceGit.AppDir
cp resources/appimage/sourcegit.appdata.xml SourceGit.AppDir/usr/share/metainfo/com.sourcegit_scm.SourceGit.appdata.xml

ARCH="$appimage_arch" ./appimagetool -v --runtime-file appimage_runtime SourceGit.AppDir "sourcegit-$VERSION.linux.$arch.AppImage"

mkdir -p resources/deb/opt/sourcegit/
mkdir -p resources/deb/usr/bin
mkdir -p resources/deb/usr/share/applications
mkdir -p resources/deb/usr/share/icons
cp -f SourceGit/* resources/deb/opt/sourcegit
ln -sf ../../opt/sourcegit/sourcegit resources/deb/usr/bin
cp -r resources/_common/applications resources/deb/usr/share
cp -r resources/_common/icons resources/deb/usr/share
sed -i -e "s/^Version:.*/Version: $VERSION/" -e "s/^Architecture:.*/Architecture: $arch/" resources/deb/DEBIAN/control
dpkg-deb --root-owner-group --build resources/deb "sourcegit_$VERSION-1_$arch.deb"

rpmbuild -bb --target="$target" resources/rpm/SPECS/build.spec --define "_topdir $(pwd)/resources/rpm" --define "_version $VERSION"
mv "resources/rpm/RPMS/$target/sourcegit-$VERSION-1.$target.rpm" ./
