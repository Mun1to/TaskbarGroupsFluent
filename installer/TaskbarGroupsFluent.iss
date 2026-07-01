; Inno Setup script for Taskbar Groups Fluent.
; Builds a per-user, one-click installer that also ensures the .NET 8 Desktop
; Runtime is present (downloading it silently if missing). Pass the published
; app folder, output folder and version on the command line, e.g.:
;   ISCC /DSourceDir="...\stage\TaskbarGroupsFluent" /DOutputDir="...\dist" /DAppVersion=1.4.0 TaskbarGroupsFluent.iss

#ifndef AppVersion
  #define AppVersion "1.4.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\pub\TaskbarGroupsFluent"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#define AppName "Taskbar Groups Fluent"
#define AppExe "TaskbarGroups.App.exe"
#define AppPublisher "Mun1to"
#define AppUrl "https://github.com/Mun1to/TaskbarGroupsFluent"

[Setup]
AppId={{B3F1C0A2-9D74-4E85-A6C1-2F5E7A9B4D30}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\TaskbarGroupsFluent
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=TaskbarGroupsFluent-Setup
SetupIconFile=..\src\assets\Icon.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall

[Code]
var
  DownloadPage: TDownloadWizardPage;

// True if any Microsoft.WindowsDesktop.App 8.x shared framework folder exists.
function IsDotNet8DesktopInstalled(): Boolean;
var
  Fr: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), Fr) then
  begin
    Result := True;
    FindClose(Fr);
    Exit;
  end;
  if FindFirst(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), Fr) then
  begin
    Result := True;
    FindClose(Fr);
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    'Preparing Taskbar Groups Fluent',
    'Downloading the .NET 8 Desktop Runtime it needs to run…',
    @OnDownloadProgress);
end;

// Before copying files, make sure the .NET 8 Desktop Runtime is present; if not,
// download the official installer and run it silently.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if IsDotNet8DesktopInstalled() then
    Exit;

  DownloadPage.Clear;
  DownloadPage.Add(
    'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe',
    'windowsdesktop-runtime-8-x64.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := 'Could not download the .NET 8 Desktop Runtime. Install it from ' +
                'https://dotnet.microsoft.com/download/dotnet/8.0 and run setup again.';
      Exit;
    end;
    if not Exec(ExpandConstant('{tmp}\windowsdesktop-runtime-8-x64.exe'),
               '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      Result := 'The .NET 8 Desktop Runtime installer could not be started.';
  finally
    DownloadPage.Hide;
  end;
end;
