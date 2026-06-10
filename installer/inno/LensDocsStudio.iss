; Lens Docs Studio Inno Setup installer MVP.
; Builds from the certified Windows folder package produced by scripts/windows/Build-WindowsPackage.ps1.

#ifndef AppVersion
  #define AppVersion "0.1.0-dev"
#endif

#ifndef PackageSource
  #define PackageSource "..\..\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev"
#endif

#ifndef OutputDir
  #define OutputDir "..\..\artifacts\installers\inno"
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "LensDocsStudio.Windows-0.1.0-dev-Setup"
#endif

#define AppName "Lens Docs Studio"
#define AppPublisher "Lens Docs Studio"
#define AppExeName "LensDocsStudio.Windows.exe"
#define AppId "{{E5D86651-4B27-4C59-B7D4-2EA98F2F8D26}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Lens Docs Studio
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
ChangesAssociations=yes
WizardStyle=modern
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a Desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "fileassoc"; Description: "Register supported Markdown, Mermaid, and text file types for this user"; GroupDescription: "Optional per-user file associations:"; Flags: unchecked

[Files]
Source: "{#PackageSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Markdown"; ValueType: string; ValueName: ""; ValueData: "Lens Docs Studio Markdown document"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Markdown\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"",0"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Markdown\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Mermaid"; ValueType: string; ValueName: ""; ValueData: "Lens Docs Studio Mermaid document"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Mermaid\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"",0"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Mermaid\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Text"; ValueType: string; ValueName: ""; ValueData: "Lens Docs Studio text document"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Text\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"",0"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Text\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.md"; ValueType: string; ValueName: ""; ValueData: "LensDocsStudio.Markdown"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Markdown"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.markdown"; ValueType: string; ValueName: ""; ValueData: "LensDocsStudio.Markdown"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Markdown"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.mmd"; ValueType: string; ValueName: ""; ValueData: "LensDocsStudio.Mermaid"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.mmd\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Mermaid"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.mermaid"; ValueType: string; ValueName: ""; ValueData: "LensDocsStudio.Mermaid"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.mermaid\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Mermaid"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.txt"; ValueType: string; ValueName: ""; ValueData: "LensDocsStudio.Text"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.txt\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Text"; Flags: uninsdeletevalue; Tasks: fileassoc

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
