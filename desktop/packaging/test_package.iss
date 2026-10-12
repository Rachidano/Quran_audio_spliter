; Script de vérification préliminaire Inno Setup pour L00-07
; Assistant d'installation en arabe RTL pour Windows x64

[Setup]
AppName=Quran Video Studio
AppVersion=0.1.0
DefaultDirName={autopf}\QuranVideoStudio
DefaultGroupName=Quran Video Studio
OutputDir=.
OutputBaseFilename=TestSetupMinimal
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Files]
; Fichier factice minimal de vérification
Source: "test_package.iss"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Quran Video Studio"; Filename: "{app}\test_package.iss"
