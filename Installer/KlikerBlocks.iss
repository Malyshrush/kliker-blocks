#define AppVersion "1.2.0"
#define AppName "Kliker Blocks"
#ifndef SmokeTest
  #define SmokeTest "0"
#endif
#ifndef BuildArch
  #define BuildArch "x64"
#endif
#if BuildArch == "x86"
  #define AllowedArchitectures "x86compatible"
#elif BuildArch == "x64"
  #define AllowedArchitectures "x64compatible"
#else
  #error Unsupported BuildArch
#endif

[Setup]
#if SmokeTest == "1"
AppId={{23D049D3-A67B-4E3E-953B-87EF8E6B1A19}
#else
AppId={{5D2B46B1-33F0-4290-A73B-AD3D966D5E44}
#endif
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Kliker Blocks
DefaultDirName={localappdata}\Programs\Kliker Blocks
#if SmokeTest == "1"
DefaultGroupName=Kliker Blocks Smoke Test
#else
DefaultGroupName=Kliker Blocks
#endif
DisableProgramGroupPage=yes
#if SmokeTest == "1"
OutputDir=..\obj\install-smoke-v120
OutputBaseFilename=KlikerBlocks-Smoke-{#AppVersion}-{#BuildArch}
#else
OutputDir=..\publish-v120
OutputBaseFilename=KlikerBlocks-Setup-{#AppVersion}-{#BuildArch}
#endif
SetupIconFile=..\Assets\Kliker.ico
UninstallDisplayIcon={app}\Kliker.Blocks.exe
#if SmokeTest == "1"
Compression=none
SolidCompression=no
#else
Compression=lzma2/max
SolidCompression=yes
#endif
DiskSpanning=no
PrivilegesRequired=lowest
ArchitecturesAllowed={#AllowedArchitectures}
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage
UsePreviousLanguage=no
WizardStyle=modern

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
en.DesktopIcon=Create a desktop shortcut
ru.DesktopIcon=Создать значок на рабочем столе
en.Autostart=Start Kliker Blocks when I sign in to Windows
ru.Autostart=Запускать Kliker Blocks при входе в Windows
en.TaskbarTip=Show how to pin Kliker Blocks to the taskbar
ru.TaskbarTip=Показать, как закрепить Kliker Blocks на панели задач
en.TaskbarInstructions=To pin Kliker Blocks, start the app, right-click its taskbar icon and choose "Pin to taskbar". Windows requires this to be your action; the installer cannot pin it automatically.
ru.TaskbarInstructions=Чтобы закрепить Kliker Blocks, запустите программу, нажмите правой кнопкой на её значке на панели задач и выберите «Закрепить на панели задач». Windows требует вашего действия; установщик не закрепляет программу автоматически.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked
Name: "autostart"; Description: "{cm:Autostart}"; Flags: unchecked
Name: "taskbartip"; Description: "{cm:TaskbarTip}"; Flags: unchecked

[Files]
Source: "..\installer-payload-v120-{#BuildArch}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
#if SmokeTest == "1"
Name: "{autoprograms}\Kliker Blocks Smoke Test"; Filename: "{app}\Kliker.Blocks.exe"
Name: "{userdesktop}\Kliker Blocks Smoke Test"; Filename: "{app}\Kliker.Blocks.exe"; Tasks: desktopicon
#else
Name: "{autoprograms}\Kliker Blocks"; Filename: "{app}\Kliker.Blocks.exe"
Name: "{userdesktop}\Kliker Blocks"; Filename: "{app}\Kliker.Blocks.exe"; Tasks: desktopicon
#endif

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "KlikerBlocks"; ValueData: """{app}\Kliker.Blocks.exe"" --windows-startup"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Kliker.Blocks.exe"; Description: "{cm:LaunchProgram,Kliker Blocks}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('taskbartip') then
    MsgBox(ExpandConstant('{cm:TaskbarInstructions}'), mbInformation, MB_OK);
end;
