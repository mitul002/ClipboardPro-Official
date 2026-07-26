; ClipboardPro - Professional Clipboard Manager
; Premium Standard Inno Setup Script
; Optimized for Cross Tech || Magnetieght EU

#define AppName "ClipboardPro"
#define AppVersion "1.4.0"
#define AppPublisher "Cross Tech"
#define AppDeveloper "Magnetieght EU"
#define AppURL "https://github.com/mitul002"
#define AppExeName "ClipboardPro.exe"

[Setup]
AppId={{C1B0A2D3-E4F5-4321-B987-6D5C4B3A2E1F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=admin
OutputDir=.
OutputBaseFilename=ClipboardPro
SetupIconFile=..\ClipboardPro.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
AppMutex=ClipboardPro_Mutex_Global
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoTextVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "shareicon"; Description: "Create a desktop shortcut for Local Share"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Start {#AppName} automatically on Windows startup"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Source paths use ..\ to reach the root project files
Source: "Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\ClipboardPro.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Share.ico"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autoprograms}\Local Share"; Filename: "{app}\{#AppExeName}"; Parameters: "--share"; WorkingDir: "{app}"; IconFilename: "{app}\Share.ico"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{autodesktop}\Local Share"; Filename: "{app}\{#AppExeName}"; Parameters: "--share"; WorkingDir: "{app}"; Tasks: shareicon; IconFilename: "{app}\Share.ico"

[Registry]
; Standard Auto-Start with Windows configuration
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

; Standard App Paths registration so ClipboardPro can be launched via Win+R Run Dialog
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletekey

[Run]
; Auto-install .NET 10.0 Desktop Runtime if missing and user confirmed
Filename: "powershell.exe"; \
    Parameters: "-ExecutionPolicy Bypass -WindowStyle Hidden -Command ""& {{ $url = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe'; $out = Join-Path $env:TEMP 'dotnet-setup.exe'; Write-Host 'Downloading...'; Invoke-WebRequest -Uri $url -OutFile $out; Start-Process -FilePath $out -Args '/install /quiet /norestart' -Wait; Remove-Item $out }}"""; \
    StatusMsg: "Downloading and installing .NET 10.0 Desktop Runtime (Required)..."; \
    Check: UserConfirmedDotNet; \
    Flags: runhidden

Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

[Code]
var
  DotNetUserConfirmed: Boolean;
  DotNetAsked: Boolean;

// Function to check if .NET 10 Desktop Runtime is installed
function NeedsDotNet10(): Boolean;
var
  Version: String;
begin
  // Check registry for .NET 10.0 Desktop Runtime (x64)
  if RegQueryStringValue(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', '10.0.0', Version) then
    Result := False
  else
    Result := True;
end;

function UserConfirmedDotNet(): Boolean;
begin
  if not NeedsDotNet10() then
  begin
    Result := False;
    Exit;
  end;

  if not DotNetAsked then
  begin
    if MsgBox('{#AppName} requires the .NET 10.0 Desktop Runtime to function.' + #13#10 + #13#10 +
              'Would you like to download and install it automatically now? (Recommended)', mbConfirmation, MB_YESNO) = IDYES then
      DotNetUserConfirmed := True
    else
      DotNetUserConfirmed := False;
    
    DotNetAsked := True;
  end;

  Result := DotNetUserConfirmed;
end;

procedure InitializeWizard();
var
  CustomLabel: TLabel;
begin
  if not WizardSilent then
  begin
    // Branding Label at bottom-left
    CustomLabel := TLabel.Create(WizardForm);
    CustomLabel.Parent := WizardForm;
    CustomLabel.Left := ScaleX(10);
    CustomLabel.Top := WizardForm.ClientHeight - ScaleY(25);
    CustomLabel.Caption := 'Developed by ' + '{#AppDeveloper}' + ' || ' + '{#AppPublisher}';
    CustomLabel.Font.Color := clGrayText;
    CustomLabel.Font.Name := 'Segoe UI';
    CustomLabel.Font.Size := 8;
  end;
end;

function InitializeUninstall(): Boolean;
var
  ErrorCode: Integer;
begin
  // Safe kill the process if running
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#AppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Result := True;
end;

procedure CurUninstallStepChanged(UninstallStep: TUninstallStep);
var
  AppDataPath: string;
  RegKey: string;
begin
  if UninstallStep = usUninstall then
  begin
    // Remove auto-start registry entry if it exists
    RegKey := 'Software\Microsoft\Windows\CurrentVersion\Run';
    if RegValueExists(HKCU, RegKey, '{#AppName}') then
      RegDeleteValue(HKCU, RegKey, '{#AppName}');

    // Prompt user to delete their saved history and settings
    AppDataPath := ExpandConstant('{userappdata}\{#AppName}');
    if DirExists(AppDataPath) then
    begin
      if MsgBox('Do you want to delete your saved clipboard history and settings?' + #13#10 +
                '(Selecting No will keep your data for future re-installation)', 
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(AppDataPath, True, True, True);
      end;
    end;
  end;

  if UninstallStep = usPostUninstall then
  begin
    // Force-clean the install directory in case any files were left
    DelTree(ExpandConstant('{app}'), True, True, True);
  end;
end;

function InitializeSetup(): Boolean;
begin
  DotNetAsked := False;
  DotNetUserConfirmed := False;
  Result := True;
end;

