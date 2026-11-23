<#
.SYNOPSIS
Gera o pacote da POC para quem vai testar com um scanner fisico.

.DESCRIPTION
Publica o agente .NET como executavel autocontido (nao exige .NET instalado), copia o build
do frontend para a pasta wwwroot do agente e compacta tudo em artifacts/poc-scanner-win-x64.zip.
Quem recebe o pacote so extrai o zip e abre ScannerAgent.exe; Node.js nao e necessario.
#>
[CmdletBinding()]
param(
	[string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$packageName = "poc-scanner-$Runtime"
$artifacts = Join-Path $root 'artifacts'
$output = Join-Path $artifacts $packageName
$zip = Join-Path $artifacts "$packageName.zip"

# The dotnet on PATH can be a runtime-only install, so take the first one that has an SDK.
$dotnet = @(
	(Get-Command dotnet -ErrorAction SilentlyContinue).Source,
	(Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe')
) | Where-Object { $_ -and (Test-Path $_) -and (& $_ --list-sdks 2>$null) } | Select-Object -First 1
if (-not $dotnet) {
	throw '.NET SDK 10 nao encontrado. Veja os pre-requisitos no README.'
}

function Invoke-Step([string]$Description, [scriptblock]$Command) {
	Write-Host "==> $Description"
	& $Command
	if ($LASTEXITCODE -ne 0) {
		throw "Falhou: $Description"
	}
}

if (Test-Path $output) { Remove-Item -Recurse -Force $output }
if (Test-Path $zip) { Remove-Item -Force $zip }

$web = Join-Path $root 'web'
Invoke-Step 'Instalando dependencias do frontend' { npm --prefix $web ci }
Invoke-Step 'Gerando o build do frontend' { npm --prefix $web run build }

$project = Join-Path $root 'agent\src\ScannerAgent\ScannerAgent.csproj'
Invoke-Step 'Publicando o agente autocontido' {
	& $dotnet publish $project -c Release -r $Runtime --self-contained true -o $output
}

Copy-Item -Recurse (Join-Path $web 'dist') (Join-Path $output 'wwwroot')
Copy-Item (Join-Path $PSScriptRoot 'LEIA-ME.txt') $output

Write-Host '==> Compactando'
# Compress-Archive and ZipFile in Windows PowerShell 5.1 write backslashes in entry names, which other
# tools misread. The tar.exe that ships with Windows 10 and 11 writes a standard zip.
& (Join-Path $env:SystemRoot 'System32\tar.exe') -a -c -f $zip -C $output *
if ($LASTEXITCODE -ne 0) {
	throw 'Falhou: Compactando'
}

$size = '{0:N1} MB' -f ((Get-Item $zip).Length / 1MB)
Write-Host "Pacote gerado: $zip ($size)"
