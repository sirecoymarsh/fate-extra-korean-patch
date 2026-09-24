param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework 4.8 compiler is required.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $compiler /nologo /target:winexe /platform:x64 "/win32manifest:$PSScriptRoot\app.manifest" "/out:$OutputDirectory\FateExtraLauncher.exe" /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\Core.cs" "$PSScriptRoot\UiPack.cs" "$PSScriptRoot\Discovery.cs" "$PSScriptRoot\Launcher.cs" "$PSScriptRoot\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed.' }
Write-Output "Built: $OutputDirectory\FateExtraLauncher.exe"
Write-Output 'Use the official binary ZIP tools directory (with tools.json and licenses) next to the EXE.'
