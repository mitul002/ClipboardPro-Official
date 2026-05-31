$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = 'clipboardpro'
  fileType      = 'exe'
  url           = 'https://clipboardpro.vercel.app/ClipboardPro-Setup.exe'
  silentArgs    = '/SILENT /NORESTART /SP-'
  validExitCodes= @(0)
  softwareName  = 'Clipboard Pro'
  checksum      = 'A433013A85311FB8D17CC9FBE03B09159EFFC9267D633F9CCB70E9C936CA5AD2'
  checksumType  = 'sha256'
}

Install-ChocolateyPackage @packageArgs
