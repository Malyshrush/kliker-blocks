#define AppVersion "1.1.7"
#define AppName "Kliker Blocks"
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
AppId={{5D2B46B1-33F0-4290-A73B-AD3D966D5E44}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Kliker Blocks
DefaultDirName={localappdata}\Programs\Kliker Blocks
DefaultGroupName=Kliker Blocks
DisableProgramGroupPage=yes
OutputDir=..\publish-v117
OutputBaseFilename=KlikerBlocks-Setup-{#AppVersion}-{#BuildArch}
SetupIconFile=..\Assets\Kliker.ico
UninstallDisplayIcon={app}\Kliker.Blocks.exe
Compression=lzma2/max
SolidCompression=yes
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
Source: "..\installer-payload-v117-{#BuildArch}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Kliker Blocks"; Filename: "{app}\Kliker.Blocks.exe"
Name: "{userdesktop}\Kliker Blocks"; Filename: "{app}\Kliker.Blocks.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "KlikerBlocks"; ValueData: """{app}\Kliker.Blocks.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Kliker.Blocks.exe"; Description: "{cm:LaunchProgram,Kliker Blocks}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('taskbartip') then
    MsgBox(ExpandConstant('{cm:TaskbarInstructions}'), mbInformation, MB_OK);
end;
