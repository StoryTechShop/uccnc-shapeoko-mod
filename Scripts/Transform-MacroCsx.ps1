#requires -PSEdition Core
<#
.Description
 Create-UCCNC-MacroCsx Create symlink of UCCNC Macros with a csx (csharp script) file extension
#>

$ErrorActionPreference = 'Stop'

$root = Join-Path -Path $PSScriptRoot -ChildPath '..' -Resolve

$csxMacros = Join-Path -Path $root -ChildPath 'Scripts/Macros' -Resolve
$profileMacros = Join-Path -Path $root -ChildPath 'UCCNC/Profiles/Macro_UB1' -Resolve

Get-ChildItem -Path $csxMacros -Filter "*.csx" | ForEach-Object -Process {
    $macroFile = Join-Path -Path $profileMacros -ChildPath ([System.IO.Path]::ChangeExtension($_.Name, '.txt'))

    $events = $false
    (Get-Content -Path $_.FullName -Raw) `
        -ireplace "(?m)^(#define DEBUG_CSX)","// GENERATED FILE (Source: $($_.Name))" `
        -ireplace '(?m)^#if DEBUG_CSX(?:.*\s)*?^#endif','' `
        -split '\n' | ForEach-Object -Process {
            $line = $_.TrimEnd();
            if(-not $events){
                $events = $line -match '#Events'
            }
            if($events){
                return ($line -replace '^ {2}','')
            }else{
                return ($line -replace '^ {4}','')
            }
        } | Join-String -Separator ([System.Environment]::NewLine) `
        | Set-Content -Path $macroFile -Force
    
    Set-ItemProperty -Path $macroFile -Force -Name 'IsReadOnly' -value $true

    Write-Output "Transformed $($_.FullName) to $([System.IO.Path]::GetRelativePath($_.FullName,$macroFile))"
}