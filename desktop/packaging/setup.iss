; =====================================================================
; Inno Setup Script - صانع فيديو القرآن الكريم (رواية ورش عن نافع)
; Version 1.0.0 - Windows x64 Native Arabic RTL Installer
; =====================================================================

#define MyAppName "صانع فيديو القرآن الكريم"
#define MyAppVersion "4.0.0"
#define MyAppPublisher "ITComp"
#define MyAppExeName "QuranVideo.exe"

[Setup]
AppId={{E68A9D71-2B19-4F54-94E5-C80DA17835B1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\QuranVideo
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=Setup
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
DisableProgramGroupPage=yes
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; GroupDescription: "اختصارات إضافية:"

[Files]
; 1. Application self-contained binaries (includes .NET 9 runtime)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; 2. Bundled FFmpeg & FFprobe tools
Source: "C:\ffmpeg\bin\ffmpeg.exe"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "C:\ffmpeg\bin\ffprobe.exe"; DestDir: "{app}\tools"; Flags: ignoreversion

; 3. Official Warsh Mushaf Pack (604 pages, bounding boxes, manifest)
Source: "..\..\data\mushaf\editions\marw-newMushaf\20260913-full\*"; DestDir: "{app}\pack"; Flags: ignoreversion recursesubdirs createallsubdirs

; 4. Default Audio Inventory for Cheikh Saber El-Hakam (114 Surahs on Archive.org)
Source: "..\..\data\reference\archive_org_114_inventory.json"; DestDir: "{app}\pack"; Flags: ignoreversion

; 5. Official Warsh Quranic Texts (6214 Ayat full text index)
Source: "..\..\data\reference\warsh-qpc-v10\source.json"; DestDir: "{app}\pack"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\إلغاء التثبيت"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "تشغيل {#MyAppName} الآن"; Flags: nowait postinstall skipifsilent
