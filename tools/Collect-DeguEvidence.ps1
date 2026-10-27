#requires -Version 5.1
<#
.SYNOPSIS
Collect evidence for a controlled Sirroh Degu 0.2.5 vs 0.2.9 comparison.
.DESCRIPTION
READ-ONLY. Does not start Valheim, change mods, change saves or contact a server.
Run AFTER a game session with several newly spawned wild Degu.
Run separately for v0.2.5 and v0.2.9 using the same game/Overhaul setup.
This script can collect logs and DLL hashes; it CANNOT read live ZDO,
renderer state, Harmony patch state or prove that any gameplay fix works.
Review logs for personal data (paths, names, IP addresses) before sharing.
.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\tools\Collect-DeguEvidence.ps1 -GameDir "C:\Games\Valheim" -Label "degu-025" -IncludeDll
.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\tools\Collect-DeguEvidence.ps1 -GameDir "C:\Games\Valheim" -Label "degu-029" -IncludeDll
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $GameDir,

    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string] $Label = 'degu-check',

    [string] $ServerLog = '',

    [switch] $IncludeDll,

    [string] $OutputDir = '.'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $GameDir).Path.TrimEnd('\', '/')
$plugins = Join-Path $root 'BepInEx\plugins'
if (-not (Test-Path -LiteralPath $plugins -PathType Container)) {
    throw "BepInEx/plugins not found at $plugins. Pass the game or mod-manager profile root."
}
if (-not (Test-Path -LiteralPath $OutputDir -PathType Container)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$destination = (Resolve-Path -LiteralPath $OutputDir).Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$zip = Join-Path $destination ("SirrohDegu-evidence-" + $Label + "-" + $stamp + ".zip")
$stage = Join-Path $env:TEMP ("SirrohDegu-evidence-" + $Label + "-" + $stamp + "-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null

function Copy-EvidenceFile([string] $Source, [string] $Name) {
    if ($Source -and (Test-Path -LiteralPath $Source -PathType Leaf)) {
        Copy-Item -LiteralPath $Source -Destination (Join-Path $stage $Name) -Force
        return $true
    }
    return $false
}

try {
    $dlls = @(Get-ChildItem -LiteralPath $plugins -Filter '*.dll' -File -Recurse |
        Sort-Object FullName)
    $manifest = @(
        foreach ($dll in $dlls) {
            $relative = $dll.FullName.Substring($root.Length).TrimStart('\', '/')
            $digest = (Get-FileHash -LiteralPath $dll.FullName -Algorithm SHA256).Hash
            [pscustomobject]@{
                relative_path = $relative
                size_bytes = $dll.Length
                sha256 = $digest
            }
        }
    )
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'dll-hashes.json') -Encoding UTF8
    $deguDlls = @($dlls | Where-Object { $_.Name -ieq 'SirrohDegu.dll' })
    $suspectedOverhaul = @($dlls | Where-Object {
        $_.Name -match '(?i)sirroh|overhaul|pracafizyczna' -and $_.Name -ine 'SirrohDegu.dll'
    })

    if ($IncludeDll) {
        $binaryDir = Join-Path $stage 'binaries'
        New-Item -ItemType Directory -Path $binaryDir -Force | Out-Null
        foreach ($dll in @($deguDlls) + @($suspectedOverhaul)) {
            $relative = $dll.FullName.Substring($root.Length).TrimStart('\', '/')
            $safeName = $relative -replace '[\\/:*?"<>|]', '_'
            Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $binaryDir $safeName) -Force
        }
    }

    $logLocations = [ordered]@{
        bepinex_log = (Join-Path $root 'BepInEx\LogOutput.log')
        player_log = (Join-Path $env:USERPROFILE 'AppData\LocalLow\IronGate\Valheim\Player.log')
        player_previous_log = (Join-Path $env:USERPROFILE 'AppData\LocalLow\IronGate\Valheim\Player-prev.log')
    }
    $found = [ordered]@{}
    foreach ($key in $logLocations.Keys) {
        $found[$key] = Copy-EvidenceFile $logLocations[$key] ($key + '.txt')
    }
    if ($ServerLog) {
        $found['server_log'] = Copy-EvidenceFile $ServerLog 'server_log.txt'
    }

    $summary = [pscustomobject]@{
        timestamp_utc = (Get-Date).ToUniversalTime().ToString('o')
        label = $Label
        profile_root = $root
        plugin_dll_count = $dlls.Count
        degu_dll_count = $deguDlls.Count
        degu_dll_relative_paths = @($deguDlls | ForEach-Object { $_.FullName.Substring($root.Length).TrimStart('\', '/') })
        potential_overhaul_dll_relative_paths = @($suspectedOverhaul | ForEach-Object { $_.FullName.Substring($root.Length).TrimStart('\', '/') })
        log_files_found = $found
        actual_dlls_included = [bool]$IncludeDll
        note = 'Only logs and on-disk files are collected. No ZDO or live renderer validation.'
    }
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'summary.json') -Encoding UTF8

    $instructions = @'
Sirroh Degu: evidence only; not an automated gameplay test.

A/B protocol:
1. Use a COPY of the same world and the same Valheim, BepInEx, Jotunn and Overhaul build.
2. Install EXACTLY ONE SirrohDegu.dll. Do not mix versions in plugin folders.
3. Start a fresh game session, spawn at least 12 NEW wild Degu, inspect coat/eyes.
4. Close the game and immediately run this tool with Label degu-025 or degu-029.
5. Repeat with the other Degu version and otherwise identical environment.
6. Attach BOTH ZIPs to the bug investigation, after reviewing logs for private data.

Check the following startup messages and their order in bepinex_log:
  Sirroh Degu v... loaded
  Overhaul v0.22.65 adapter active
  Overhaul compatibility disabled / failed preflight/patch
  Degu owns one ZDO-authoritative coat/eye assignment
  Degu REGISTERED + VERIFIED
  Degu: PlainsMonsters faction
  Degu Plains natural spawn registered

If startup logs do NOT settle the issue, subsequent diagnostics must measure
actual fresh-spawn ZDO values (furRandomizerSetActive, furRandomizerSet,
sirroh.wolf.eye.v1), ownership, DeguVariantAssignment presence, active
renderer materials and Harmony patch status. None are automatically
available in these files. Never infer gameplay success from this ZIP alone.

No game files or saves were modified by the collector.
'@
    Set-Content -LiteralPath (Join-Path $stage 'READ_ME_FIRST.txt') -Value $instructions -Encoding UTF8
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal -Force

    Write-Host ("Evidence created: " + $zip)
    Write-Host ("Degu DLL copies found: " + $deguDlls.Count)
    if ($deguDlls.Count -ne 1) {
        Write-Warning "Expected one SirrohDegu.dll. Multiple or zero copies need investigation."
    }
    if (-not $found['bepinex_log']) {
        Write-Warning "No BepInEx LogOutput.log found under supplied profile; confirm correct GameDir."
    }
    Write-Warning "Review the ZIP for names, local paths, IP addresses or IDs before sharing."
}
finally {
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}
