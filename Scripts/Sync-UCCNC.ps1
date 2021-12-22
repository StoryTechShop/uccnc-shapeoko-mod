#requires -PSEdition Core
<#
.Description
 Sync-UCCNC Synchronize UCCNC customizations from git to UCCNC install
#>
Param(
    [string]
    $UCCNCPath="$env:SystemDrive\UCCNC"
)

$source = Join-Path -Path $PSScriptRoot -ChildPath '..' -Resolve
$source = Join-Path -Path $source -ChildPath 'UCCNC' -Resolve

$destination = Resolve-Path $UCCNCPath

robocopy $source $destination *.* /s /njh /njs /nc /ns /np /ndl /fp /nodcopy /a+:R /is /it /xx /xf *.pro