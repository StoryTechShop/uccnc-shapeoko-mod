#requires -PSEdition Core
<#
.Description
 Sync-UCCNC Synchronize UCCNC customizations from git to UCCNC install
#>
Param(
    [string]
    $UCCNCPath="$env:SystemDrive\UCCNC"
)

$ErrorActionPreference = 'Stop'

$scripts = Split-Path -Path $MyInvocation.MyCommand.Path -Parent

$source = Join-Path -Path $PSScriptRoot -ChildPath '..' -Resolve
$source = Join-Path -Path $source -ChildPath 'UCCNC' -Resolve

$destination = Resolve-Path $UCCNCPath

# Transform any CSX Macros
. (Join-Path -Path $scripts -ChildPath Transform-MacroCsx.ps1 -Resolve)

# Copy Macros to UCCNC Profile
robocopy $source $destination *.* /s /njh /njs /nc /ns /np /ndl /fp /nodcopy /a+:R /is /it /xx /xf *.pro | ForEach-Object -Process { $_.Trim() }