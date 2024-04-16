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
mkdir -p $RPM_BUILD_ROOT/usr/bin
mkdir -p $RPM_BUILD_ROOT/usr/share/applications
mkdir -p $RPM_BUILD_ROOT/usr/share/icons
cp -r ../../_common/usr $RPM_BUILD_ROOT/
cp -f ../../../SourceGit/* $RPM_BUILD_ROOT/opt/sourcegit/
chmod 755 -R $RPM_BUILD_ROOT

%files
/opt
/usr/bin
/usr/share

%changelog
# skip