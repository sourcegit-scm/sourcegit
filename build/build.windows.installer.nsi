; Application properties
!define PRODUCT_NAME "SourceGit"
!define PRODUCT_VERSION ${VERSION}
!define PRODUCT_PUBLISHER "sourcegit-scm"
!define PRODUCT_WEB_SITE "https://github.com/sourcegit-scm/sourcegit"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\${PRODUCT_NAME}\SourceGit.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

; Set installation and uninstallation icons and name
!define MUI_ICON "resources\app\App.ico"
!define MUI_UNICON "resources\app\App.ico"

; Language Selection Dialog Settings
!define MUI_LANGDLL_REGISTRY_ROOT "${PRODUCT_UNINST_ROOT_KEY}"
!define MUI_LANGDLL_REGISTRY_KEY "${PRODUCT_UNINST_KEY}"
!define MUI_LANGDLL_REGISTRY_VALUENAME "NSIS:Language"

; Include language files
!include "MUI2.nsh"

; Set pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Set language
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "TradChinese"

; Component descriptions
LangString DESC_SecDesktopShortcut ${LANG_ENGLISH} "Create a desktop shortcut."
LangString DESC_SecStartMenuShortcut ${LANG_ENGLISH} "Create a start menu shortcut."

; Define some basic properties
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
Outfile "sourcegit_${VERSION}.win-x64.installer.exe"
InstallDir $PROGRAMFILES64\${PRODUCT_NAME}
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin

Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd

; Start installation section
Section "Install"

    ; Set version
    WriteRegStr HKLM "Software\${PRODUCT_NAME}" "Version" ${VERSION}

    ; Copy files to installation directory
    SetOutPath $INSTDIR
    File /r "SourceGit\*.*"
    
    CreateDirectory "$SMPROGRAMS\SourceGit"
    CreateShortCut "$SMPROGRAMS\SourceGit\SourceGit.lnk" "$INSTDIR\SourceGit.exe"
    CreateShortCut "$DESKTOP\SourceGit.lnk" "$INSTDIR\SourceGit.exe"

    WriteUninstaller "$INSTDIR\uninst.exe"
    WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\SourceGit.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" '$\"$INSTDIR\uninst.exe$\"'
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" '$\"$INSTDIR\uninst.exe$\"'
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
SectionEnd

; Start uninstallation section
Section "Uninstall"

    ; Delete all files and directories
    RMDir /r $INSTDIR

    Delete "$DESKTOP\SourceGit.lnk"
    Delete "$SMPROGRAMS\SourceGit\SourceGit.lnk"
    RMDir "$SMPROGRAMS\SourceGit"

    ; Delete uninstallation information
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
    DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"

SectionEnd
