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
SetupIconFile=..\..\src\native\LensDocsStudio.Native\Resources\Brand\application.ico
SetupLogging=yes
AppMutex=Local\LensDocsStudio.Native.Running
CloseApplications=no
RestartApplications=no
DisableWelcomePage=no
InfoBeforeFile=..\..\docs\open-source\privacy.md

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
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent; Check: UpdateChoiceWritten

[Messages]
english.WelcomeLabel1=Welcome to the [name] installation wizard
english.WelcomeLabel2=Version {#AppVersion}%n%nLocal Markdown, Mermaid, and documentation studio.%n%nThis internal preview installs alongside the existing Windows edition. Workspace documents and application state are preserved during uninstall.
english.InfoBeforeLabel=Please read the local privacy policy. No Internet connection is required. The next page lets you choose automatic update checks before first launch.

[Code]
var
  PrivacyPage: TWizardPage;
  KeepSavedChoice, AutomaticUpdates: TNewCheckBox;
  PreferencesHash: String;
  InstallationCompleted, ChoiceCommitted, ChoiceFailed: Boolean;

function MoveFileEx(ExistingName, NewName: String; Flags: Cardinal): Boolean;
  external 'MoveFileExW@kernel32.dll stdcall';

function StateFile(Name: String): String;
begin
  Result := ExpandConstant('{localappdata}\LensDocsStudio.Native\') + Name;
end;

procedure KeepSavedChanged(Sender: TObject);
begin
  AutomaticUpdates.Enabled := not KeepSavedChoice.Checked;
end;

procedure InitializeWizard;
var
  Notice: TNewMemo;
begin
  PreferencesHash := 'missing';
  try
    if FileExists(StateFile('preferences.json')) then
      PreferencesHash := GetSHA256OfFile(StateFile('preferences.json'));
  except
    PreferencesHash := 'unreadable';
  end;
  PrivacyPage := CreateCustomPage(wpInfoBefore, 'Privacy and updates', 'Choose automatic update checks before the application starts.');
  Notice := TNewMemo.Create(PrivacyPage);
  Notice.Parent := PrivacyPage.Surface;
  Notice.SetBounds(0, 0, PrivacyPage.SurfaceWidth, PrivacyPage.SurfaceHeight - ScaleY(72));
  Notice.ReadOnly := True;
  Notice.ScrollBars := ssVertical;
  Notice.WordWrap := True;
  Notice.Text := 'Documents and workspaces are handled locally. Settings, recent paths, drafts and recovery information are stored in your local application data; this storage is not encrypted by the application.' + #13#10#13#10 +
    'If enabled, automatic checks request GitHub Releases metadata (and possibly native-update.json). Manual Check now remains available when automatic checks are off. Downloads and installation are separate user choices.' + #13#10#13#10 +
    'Confirmed external links open your default application. Download and insert contacts the HTTPS image address you choose. Windows may contact certificate and trust services during future signed operations. This preview has no configured production signing identity.' + #13#10#13#10 +
    'Setup installs per-user files, an uninstaller and a Start Menu shortcut. Desktop shortcuts and Open with associations are optional. Uninstall retains your documents and private application state. The preceding page contains the full local privacy policy; a copy is included as PRIVACY.md.' + #13#10#13#10 +
    'New installations default to automatic checks off. Keep a saved choice on upgrade, or clear the keep option to make a new choice. You can change it later in Help > Updates. Silent setup preserves valid saved preferences, otherwise checks stay off.';
  KeepSavedChoice := TNewCheckBox.Create(PrivacyPage);
  KeepSavedChoice.Parent := PrivacyPage.Surface;
  KeepSavedChoice.SetBounds(0, Notice.Height + ScaleY(12), PrivacyPage.SurfaceWidth, ScaleY(22));
  KeepSavedChoice.Caption := '&Keep my saved update preference (off if none is valid)';
  KeepSavedChoice.Checked := FileExists(StateFile('preferences.json')) or FileExists(StateFile('preferences.json.bak'));
  KeepSavedChoice.OnClick := @KeepSavedChanged;
  AutomaticUpdates := TNewCheckBox.Create(PrivacyPage);
  AutomaticUpdates.Parent := PrivacyPage.Surface;
  AutomaticUpdates.SetBounds(0, KeepSavedChoice.Top + ScaleY(28), PrivacyPage.SurfaceWidth, ScaleY(22));
  AutomaticUpdates.Caption := '&Automatically check for updates';
  AutomaticUpdates.Checked := False;
  KeepSavedChanged(nil);
end;

procedure CommitUpdateChoice;
var
  Action, Pending, Temporary, Content: String;
begin
  if ChoiceCommitted or ChoiceFailed or not InstallationCompleted then exit;
  Action := 'preserve';
  if not WizardSilent and not KeepSavedChoice.Checked then begin
    Action := 'disable';
    if AutomaticUpdates.Checked then Action := 'enable';
  end;
  Pending := StateFile('installer-update-choice.json');
  Temporary := Pending + '.tmp';
  Content := '{"SchemaVersion":1,"Action":"' + Action + '","PreferencesSha256":"' + PreferencesHash + '"}';
  { Only a completed installation writes this bounded instruction. Never edit preferences.json. }
  if ForceDirectories(ExtractFileDir(Pending)) and SaveStringToFile(Temporary, Content, False) and MoveFileEx(Temporary, Pending, 9) then
    ChoiceCommitted := True
  else begin
    ChoiceFailed := True;
    DeleteFile(Temporary);
    Log('The update choice could not be transferred; automatic application launch is blocked.');
    if not WizardSilent then MsgBox('Your update choice could not be transferred safely. Setup will not launch the application. Review Help > Updates before enabling automatic checks.', mbError, MB_OK);
  end;
end;

function UpdateChoiceWritten: Boolean;
begin
  Result := ChoiceCommitted;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    InstallationCompleted := True;
    CommitUpdateChoice;
  end;
  if CurStep = ssDone then CommitUpdateChoice;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  { The postinstall Run entry is processed after Finish. Commit first. }
  if CurPageID = wpFinished then CommitUpdateChoice;
  Result := True;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if ChoiceFailed then Result := 1;
end;
