Name: devboard
Version: %_version
Release: 1
Summary: Open-source & Free Git Gui Client
License: MIT
URL: https://devboard-scm.github.io/
Source: https://github.com/devboard-scm/devboard/archive/refs/tags/v%_version.tar.gz
Requires: libX11.so.6()(%{__isa_bits}bit)
Requires: libSM.so.6()(%{__isa_bits}bit)
Requires: libicu
Requires: xdg-utils

%define _build_id_links none

%description
Open-source & Free Git Gui Client

%install
mkdir -p %{buildroot}/opt/devboard
mkdir -p %{buildroot}/%{_bindir}
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons
cp -f %{_topdir}/../../DevBoard/* %{buildroot}/opt/devboard/
ln -rsf %{buildroot}/opt/devboard/devboard %{buildroot}/%{_bindir}
cp -r %{_topdir}/../_common/applications %{buildroot}/%{_datadir}
cp -r %{_topdir}/../_common/icons %{buildroot}/%{_datadir}
chmod 755 -R %{buildroot}/opt/devboard
chmod 755 %{buildroot}/%{_datadir}/applications/devboard.desktop

%files
%dir /opt/devboard/
/opt/devboard/*
/usr/share/applications/devboard.desktop
/usr/share/icons/*
%{_bindir}/devboard

%changelog
# skip
