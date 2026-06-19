$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = 'clipboardpro'
  fileType      = 'exe'
  url           = 'https://clipboardpro.vercel.app/ClipboardPro.exe'
  silentArgs    = '/SILENT /NORESTART /SP-'
  validExitCodes= @(0)
  softwareName  = 'Clipboard Pro'
  checksum      = '28B3018FFB8839188EC28E12BE1BD1C6828D86213CA58383E48E922EDF2AC8A7'
  checksumType  = 'sha256'
}

Install-ChocolateyPackage @packageArgs
