; ============================================================
; MultiDesktop 安装包构建脚本 (NSIS)
; 用于将 AOT 发布目录 (dotnet publish 输出) 打包为 exe 安装包
;
; 用法:
;   "D:\program\Nsis\makensis.exe" build_installer.nsi
;   或先运行同目录下的 build_installer.bat（自动执行 dotnet publish + makensis）
;
; 可覆盖的变量:
;   /DPUBLISH_DIR=...   AOT 发布输出目录（默认 ..\src\MultiDesktop\bin\Release\net10.0-windows\win-x64\publish）
;   /DICON_FILE=...     安装包图标（默认 ..\src\MultiDesktop\Desktop.ico）
;   /DVERSION=...       版本号（默认 1.3.6.0）
; ============================================================

Unicode true
; 高 DPI 支持：per-monitor v2 + 系统级双声明（NSIS 官方推荐写法），
; 高分屏文字锐利、对话框随 DPI 缩放；Win10 1703+ 用 PMv2，旧系统回退 System
ManifestDPIAware true
ManifestDPIAwareness "PerMonitorV2,System"

; ---------------- 常量 ----------------
!ifndef APP_NAME
  !define APP_NAME "MultiDesktop多桌面助手"
!endif
!ifndef APP_EXE
  !define APP_EXE "MultiDesktop.AOT.exe"
!endif
!ifndef APP_VERSION
  !define APP_VERSION "1.3.6.0"
!endif
!ifndef APP_PUBLISHER
  !define APP_PUBLISHER "Buger2008"
!endif
!ifndef APP_REGKEY
  !define APP_REGKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MultiDesktop"
!endif

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\src\MultiDesktop\bin\Release\net10.0-windows\win-x64\publish"
!endif
!ifndef ICON_FILE
  !define ICON_FILE "..\src\MultiDesktop\Desktop.ico"
!endif

; ---------------- 安装包基本信息 ----------------
Name "${APP_NAME} ${APP_VERSION}"
OutFile "MultiDesktop-Setup-${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES64\MultiDesktop"
InstallDirRegKey HKLM "${APP_REGKEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma

; 图标（使用透明背景的新图标）
Icon "${ICON_FILE}"
UninstallIcon "${ICON_FILE}"

; 版本信息
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "FileDescription" "${APP_NAME} 安装程序"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "© ${APP_PUBLISHER}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"

; ---------------- MUI2 界面 ----------------
!include "MUI2.nsh"

!define MUI_ABORTWARNING
!define MUI_ICON "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "立即运行 ${APP_NAME}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; 语言（简体中文 + 英语）
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

; ---------------- 安装段 ----------------
Section "Install" SecInstall
  SetOutPath "$INSTDIR"

  ; 打包 AOT 发布目录下的所有文件（排除调试符号）
  File /r /x "*.pdb" "${PUBLISH_DIR}\*"

  ; 卸载程序
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; 快捷方式：桌面 + 开始菜单
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\卸载 ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"

  ; 注册表：添加/删除程序
  WriteRegStr HKLM "${APP_REGKEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${APP_REGKEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${APP_REGKEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${APP_REGKEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${APP_REGKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${APP_REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKLM "${APP_REGKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${APP_REGKEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${APP_REGKEY}" "EstimatedSize" 51200
SectionEnd

; ---------------- 卸载段 ----------------
Section "Uninstall"
  ; 结束正在运行的进程
  nsExec::ExecToLog 'taskkill /IM "${APP_EXE}" /F'

  ; 删除快捷方式
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\卸载 ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  ; 删除注册表
  DeleteRegKey HKLM "${APP_REGKEY}"
  DeleteRegKey HKCU "Software\${APP_NAME}"

  ; 删除安装目录
  RMDir /r "$INSTDIR"
SectionEnd
