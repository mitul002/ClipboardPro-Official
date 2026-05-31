$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = 'clipboardpro'
  fileType      = 'exe'
  url           = 'https://clipboardpro.vercel.app/ClipboardPro-Setup.exe'
  silentArgs    = '/SILENT /NORESTART /SP-'
  validExitCodes= @(0)
  softwareName  = 'Clipboard Pro'
  checksum      = 'F22D0046D849DECD16FA7032FED4CF0DA2566FC0BF24B96A91CF6E55DA86D803'
  checksumType  = 'sha256'
}

Install-ChocolateyPackage @packageArgs
