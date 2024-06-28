Name: sourcegit
Version: %_version
Release: 1
Summary: Open-source & Free Git Gui Client
License: MIT
URL: https://sourcegit-scm.github.io/
Source: https://github.com/sourcegit-scm/sourcegit/archive/refs/tags/v%_version.tar.gz
Requires: libX11.so.6
Requires: libSM.so.6

%define _build_id_links none

%description
Open-source & Free Git Gui Client

%install
mkdir -p $RPM_BUILD_ROOT/opt/sourcegit
mkdir -p $RPM_BUILD_ROOT/usr/share/applications
mkdir -p $RPM_BUILD_ROOT/usr/share/icons
cp -r ../../_common/applications $RPM_BUILD_ROOT/usr/share/
cp -r ../../_common/icons $RPM_BUILD_ROOT/usr/share/
cp -f ../../../SourceGit/* $RPM_BUILD_ROOT/opt/sourcegit/
chmod 755 -R $RPM_BUILD_ROOT/opt/sourcegit
chmod 755 $RPM_BUILD_ROOT/usr/share/applications/sourcegit.desktop

%files
/opt/sourcegit
/usr/share

%post
ln -s /opt/sourcegit/sourcegit /usr/bin/sourcegit

%postun
rm -f /usr/bin/sourcegit

%changelog
# skip