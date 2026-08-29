#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

# ICU versions to support (Debian has no virtual package, must list all)
# Format: space-separated version numbers
ICU_VERSIONS="78 77 76 74 72 71 70 69 68 67 66 65 63"

arch=
appimage_arch=
target=
case "$RUNTIME" in
    linux-x64)
        arch=amd64
        appimage_arch=x86_64
        target=x86_64;;
    linux-arm64)
        arch=arm64
        appimage_arch=arm_aarch64
        target=aarch64;;
    *)
        echo "Unknown runtime $RUNTIME"
        exit 1;;
esac

APPIMAGETOOL_URL=https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage

cd build

if [[ ! -f "appimagetool" ]]; then
    curl -o appimagetool -L "$APPIMAGETOOL_URL"
    chmod +x appimagetool
fi

rm -f DevBoard/*.dbg
rm -f DevBoard/*.pdb

mkdir -p DevBoard.AppDir/opt
mkdir -p DevBoard.AppDir/usr/share/metainfo
mkdir -p DevBoard.AppDir/usr/share/applications

cp -r DevBoard DevBoard.AppDir/opt/devboard
desktop-file-install resources/_common/applications/devboard.desktop --dir DevBoard.AppDir/usr/share/applications \
    --set-icon com.devboard_scm.DevBoard --set-key=Exec --set-value=AppRun
mv DevBoard.AppDir/usr/share/applications/{devboard,com.devboard_scm.DevBoard}.desktop
cp resources/_common/icons/devboard.png DevBoard.AppDir/com.devboard_scm.DevBoard.png
ln -rsf DevBoard.AppDir/opt/devboard/devboard DevBoard.AppDir/AppRun
ln -rsf DevBoard.AppDir/usr/share/applications/com.devboard_scm.DevBoard.desktop DevBoard.AppDir
cp resources/appimage/devboard.appdata.xml DevBoard.AppDir/usr/share/metainfo/com.devboard_scm.DevBoard.appdata.xml

ARCH="$appimage_arch" ./appimagetool -v DevBoard.AppDir "devboard-$VERSION.linux.$arch.AppImage"

mkdir -p resources/deb/opt/devboard/
mkdir -p resources/deb/usr/bin
mkdir -p resources/deb/usr/share/applications
mkdir -p resources/deb/usr/share/icons
cp -a DevBoard/. resources/deb/opt/devboard/
ln -rsf resources/deb/opt/devboard/devboard resources/deb/usr/bin
cp -r resources/_common/applications resources/deb/usr/share
cp -r resources/_common/icons resources/deb/usr/share

# Calculate installed size in KB
installed_size=$(du -sk resources/deb | cut -f1)

# Generate ICU dependencies string for Debian
# Debian lacks libicu virtual package, must list all versions with OR operator
icu_deps="libicu"
for v in $ICU_VERSIONS; do
    icu_deps="$icu_deps | libicu$v"
done

# Update the control file (replace placeholder, not whole Depends line)
sed -i -e "s/^Version:.*/Version: $VERSION/" \
    -e "s/^Architecture:.*/Architecture: $arch/" \
    -e "s/^Installed-Size:.*/Installed-Size: $installed_size/" \
    -e "s/@ICU_DEPS@/$icu_deps/" \
    resources/deb/DEBIAN/control

# Build deb package with gzip compression
dpkg-deb -Zgzip --root-owner-group --build resources/deb "devboard_$VERSION-1_$arch.deb"

rpmbuild -bb --target="$target" resources/rpm/SPECS/build.spec --define "_topdir $(pwd)/resources/rpm" --define "_version $VERSION"
mv "resources/rpm/RPMS/$target/devboard-$VERSION-1.$target.rpm" ./
