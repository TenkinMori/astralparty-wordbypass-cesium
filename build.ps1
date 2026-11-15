$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

dotnet build "$root\src\WordBypassMod" -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败" }

$out = Join-Path $root 'dist\WordBypassMod'
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item "$root\src\WordBypassMod\bin\Release\netstandard2.0\WordBypassMod.dll" $out -Force
Copy-Item "$root\src\WordBypassMod\WordBypassMod.json" $out -Force
if (Test-Path "$root\icon.png") { Copy-Item "$root\icon.png" $out -Force }
Write-Host "OK -> $out"
Get-ChildItem $out | Select-Object Name, Length | Format-Table -AutoSize
