$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = 'clipboardpro'
  fileType      = 'exe'
  url           = 'https://clipboardpro.vercel.app/ClipboardPro-Setup.exe'
  silentArgs    = '/SILENT /NORESTART /SP-'
  validExitCodes= @(0)
  softwareName  = 'Clipboard Pro'
  checksum      = '8CC962EDE2537142A7E9A68751A056C5FAC9AEC27D0745E62C18E87BFDCCE099'
  checksumType  = 'sha256'
}

Install-ChocolateyPackage @packageArgs
