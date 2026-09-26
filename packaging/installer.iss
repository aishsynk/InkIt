; InkIt Professional Screen Annotation & Presentation Tool
; Inno Setup Installer Script

#define MyAppName "InkIt"
; The release script passes /DMyAppVersion=0.0.0.N; 0.0.0.0 marks a local, unreleased build.
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0.0"
#endif
#define MyAppURL "https://github.com/aishsynk/InkIt"
#define MyAppPublisher "InkIt"
#define MyAppExeName "InkIt.exe"

[Setup]
AppId={{D8158F59-B427-4638-9B7E-70C6EA663412}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=Copyright (c) 2026 Aishwar Nigam
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} Setup - draw, highlight, zoom and screenshot on your screen
VersionInfoCompany={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=InkIt_Setup_v{#MyAppVersion}
SetupIconFile=..\src\ScreenCanvas\Assets\InkIt.ico
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
MinVersion=10.0.19041

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; inkit:// links (Stream Deck, shortcuts, scripts) and double-click to open saved .inkit drawings.
Root: HKCU; Subkey: "Software\Classes\inkit"; ValueType: string; ValueName: ""; ValueData: "URL:InkIt command"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\inkit"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\inkit\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\inkit\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.inkit"; ValueType: string; ValueName: ""; ValueData: "InkIt.Drawings"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\InkIt.Drawings"; ValueType: string; ValueName: ""; ValueData: "InkIt drawings"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\InkIt.Drawings\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\InkIt.Drawings\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
; "Start with Windows" is chosen in InkIt's Settings; remove it on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "InkIt"; Flags: uninsdeletevalue dontcreatekey

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Comment: "Draw, highlight, zoom and screenshot on your screen"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Comment: "Draw, highlight, zoom and screenshot on your screen"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
