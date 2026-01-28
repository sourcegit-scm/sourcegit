Name: sourcegit
Version: %_version
Release: 1
Summary: Open-source & Free Git Gui Client
License: MIT
URL: https://sourcegit-scm.github.io/
Source: https://github.com/sourcegit-scm/sourcegit/archive/refs/tags/v%_version.tar.gz
Requires: libX11.so.6()(%{__isa_bits}bit)
Requires: libSM.so.6()(%{__isa_bits}bit)
Requires: libicu
Requires: xdg-utils

%define _build_id_links none

%description
Open-source & Free Git Gui Client

%install
mkdir -p %{buildroot}/opt/sourcegit
mkdir -p %{buildroot}/%{_bindir}
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons
cp -f %{_topdir}/../../SourceGit/* %{buildroot}/opt/sourcegit/
ln -rsf %{buildroot}/opt/sourcegit/sourcegit %{buildroot}/%{_bindir}
cp -r %{_topdir}/../_common/applications %{buildroot}/%{_datadir}
cp -r %{_topdir}/../_common/icons %{buildroot}/%{_datadir}
chmod 755 -R %{buildroot}/opt/sourcegit
chmod 755 %{buildroot}/%{_datadir}/applications/sourcegit.desktop

%files
%dir /opt/sourcegit/
/opt/sourcegit/*
/usr/share/applications/sourcegit.desktop
/usr/share/icons/*
%{_bindir}/sourcegit

%changelog
# skip
