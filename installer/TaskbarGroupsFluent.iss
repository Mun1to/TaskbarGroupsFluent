; Inno Setup script for Taskbar Groups Fluent.
; Builds a per-user, one-click installer that also ensures the .NET 10 Desktop
; Runtime is present (downloading it silently if missing). Pass the published
; app folder, output folder and version on the command line, e.g.:
;   ISCC /DSourceDir="...\stage\TaskbarGroupsFluent" /DOutputDir="...\dist" /DAppVersion=1.4.0 TaskbarGroupsFluent.iss
;
; Add /DRuntimeInstaller="...\windowsdesktop-runtime-10.x.y-win-x64.exe" for the offline
; build: the runtime travels inside the setup, which then downloads nothing. The
; Microsoft Store requires that, and it suits PCs without internet access.

#ifndef AppVersion
  #define AppVersion "1.4.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\pub\TaskbarGroupsFluent"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif
#ifdef RuntimeInstaller
  #define SetupName "TaskbarGroupsFluent-Setup-Offline"
#else
  #define SetupName "TaskbarGroupsFluent-Setup"
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
OutputBaseFilename={#SetupName}
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
#ifdef RuntimeInstaller
; Unpacked on demand by PrepareToInstall, and only when the runtime is missing.
Source: "{#RuntimeInstaller}"; DestName: "windowsdesktop-runtime-10-x64.exe"; Flags: dontcopy
#endif
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall

[Code]
var
  DownloadPage: TDownloadWizardPage;

// The hover watcher is a resident process with no window, so CloseApplications
// never sees it and its files stay locked mid-install. Stopping it here is safe:
// the app starts it again on launch whenever the setting is on.
//
// The flyout it keeps loaded near the taskbar has to go too. It outlives the
// watcher by a few seconds, holding the Background files open, and an uninstall
// that ran in that window left the Background folder behind. Killing it is safe
// for the same reason the app kills stale flyouts: it owns nothing but the panel.
procedure StopHoverWatcher();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM TaskbarGroups.Hover.exe',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM TaskbarGroups.Background.exe',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopHoverWatcher();
end;

// On the way out, take the startup entry with us. Leaving it behind would have
// Windows try to launch a watcher that is no longer on disk at every sign-in.
//
// The main window is closed too. Unlike setup, the uninstaller does not ask
// Windows to close running apps, and uninstalling with the window open left the
// install folder behind with the app's files still locked.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM TaskbarGroups.App.exe',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    StopHoverWatcher();
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'TaskbarGroupsFluentHover');
  end;
end;

// True if any Microsoft.WindowsDesktop.App 10.x shared framework folder exists.
function IsDotNetDesktopInstalled(): Boolean;
var
  Fr: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App.*'), Fr) then
  begin
    Result := True;
    FindClose(Fr);
    Exit;
  end;
  if FindFirst(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App.*'), Fr) then
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
    'Downloading the .NET 10 Desktop Runtime it needs to run…',
    @OnDownloadProgress);
end;

// Before copying files, make sure the .NET 10 Desktop Runtime is present; if not,
// download the official installer and run it silently.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if IsDotNetDesktopInstalled() then
    Exit;

#ifdef RuntimeInstaller
  // Offline build: the runtime is inside this setup.
  ExtractTemporaryFile('windowsdesktop-runtime-10-x64.exe');
  if not Exec(ExpandConstant('{tmp}\windowsdesktop-runtime-10-x64.exe'),
             '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    Result := 'The .NET 10 Desktop Runtime installer could not be started.';
  Exit;
#endif

  DownloadPage.Clear;
  DownloadPage.Add(
    'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe',
    'windowsdesktop-runtime-10-x64.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := 'Could not download the .NET 10 Desktop Runtime. Install it from ' +
                'https://dotnet.microsoft.com/download/dotnet/10.0 and run setup again.';
      Exit;
    end;
    if not Exec(ExpandConstant('{tmp}\windowsdesktop-runtime-10-x64.exe'),
               '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      Result := 'The .NET 10 Desktop Runtime installer could not be started.';
  finally
    DownloadPage.Hide;
  end;
end;
