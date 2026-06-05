; ClipboardPro - Size Optimized Setup
#define AppName "ClipboardPro-Optimized"
#define AppVersion "1.4.0"
#define AppExeName "ClipboardPro.exe"

[Setup]
AppId={{C1B0A2D3-E4F5-4321-B987-6D5C4B3A2E1F}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
OutputDir=.
OutputBaseFilename=ClipboardPro-Size-Test
Compression=lzma2/ultra64
SolidCompression=yes

[Files]
Source: "Publish_Optimized\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\ClipboardPro.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Share.ico"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
