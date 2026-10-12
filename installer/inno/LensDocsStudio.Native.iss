; Native WPF edition. Product and registry identity intentionally differ from the legacy shell.
#ifndef AppVersion
  #error AppVersion must be supplied by Build-NativePackage.ps1
#endif
#ifndef PackageSource
  #error PackageSource must be supplied by Build-NativePackage.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by Build-NativePackage.ps1
#endif
#define AppName "Lens Docs Studio Native Preview"
#define AppExeName "LensDocsStudio.Native.exe"

[Setup]
AppId={{3AF043CA-F7C1-4D1F-A8BC-847B4C44E23F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Lens Docs Studio
AppPublisherURL=https://github.com/DouglasNLima/local-docs-studio
DefaultDirName={localappdata}\Programs\Lens Docs Studio Native
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=LensDocsStudio.Native-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=lowest
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
ChangesAssociations=yes
WizardStyle=modern
SetupLogging=yes
AppMutex=Local\LensDocsStudio.Native.Running
CloseApplications=no
RestartApplications=no
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a Desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "fileassoc"; Description: "Add Native Preview to Open with for Markdown and Mermaid files"; GroupDescription: "Optional per-user file associations:"; Flags: unchecked

[Files]
Source: "{#PackageSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Native.Document"; ValueType: string; ValueName: ""; ValueData: "Lens Docs Studio native document"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Native.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"",0"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\LensDocsStudio.Native.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Native.Document"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Native.Document"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.mmd\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Native.Document"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.mermaid\OpenWithProgids"; ValueType: none; ValueName: "LensDocsStudio.Native.Document"; Flags: uninsdeletevalue; Tasks: fileassoc

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Messages]
english.WelcomeLabel1=Welcome to the [name] installation wizard
english.WelcomeLabel2=Version {#AppVersion}%n%nLocal Markdown, Mermaid, and documentation studio.%n%nThis internal preview installs alongside the existing Windows edition. Workspace documents and application state are preserved during uninstall.
