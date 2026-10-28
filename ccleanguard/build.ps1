#Requires -Version 5.1
<#
.SYNOPSIS
    Builds dist\CCleanGuard.exe with the in-box .NET Framework compiler.

.DESCRIPTION
    No SDK, no NuGet, no network. Windows 10/11 ship csc.exe and .NET Framework 4.8,
    so this builds on a clean machine.

    /codepage:65001 is what keeps the Chinese UI strings intact: csc otherwise
    assumes the system ANSI code page for BOM-less source files.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
#>
[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$distDir = Join-Path $root 'dist'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw 'csc.exe not found. This script needs the .NET Framework 4.x compiler (in-box on Windows 10/11).'
}

if ($Clean -and (Test-Path -LiteralPath $distDir)) {
    Remove-Item -LiteralPath $distDir -Recurse -Force
}
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

$sources = @(Get-ChildItem -LiteralPath $srcDir -Filter *.cs -File | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "No .cs files under $srcDir" }

$out = Join-Path $distDir 'CCleanGuard.exe'
$manifest = Join-Path $srcDir 'app.manifest'

$arguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/codepage:65001'
    '/warn:4'
    ('/out:"' + $out + '"')
    ('/win32manifest:"' + $manifest + '"')
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
)
$arguments += $sources

Write-Host ('compiler : ' + $csc)
Write-Host ('sources  : ' + $sources.Count + ' file(s)')
Write-Host ('output   : ' + $out)

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
    throw ('csc failed with exit code ' + $LASTEXITCODE)
}

$exe = Get-Item -LiteralPath $out
Write-Host ''
Write-Host ('built    : ' + $exe.FullName)
Write-Host ('size     : ' + [math]::Round($exe.Length / 1KB, 1) + ' KB')
Write-Host ('version  : ' + $exe.VersionInfo.FileVersion)
