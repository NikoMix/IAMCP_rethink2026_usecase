#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and lints infra/main.bicep, then asserts the keyless, EU-residency and least-privilege invariants
    on the compiled ARM template. Offline: no Azure sign-in or subscription is used.

.EXAMPLE
    pwsh infra/scripts/Test-Infrastructure.ps1
#>
[CmdletBinding()]
param(
    [string] $TemplatePath,
    [string] $ParametersPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# $PSScriptRoot is not available in parameter defaults on Windows PowerShell 5.1.
$infraRoot = Split-Path -Parent $PSScriptRoot
if (-not $TemplatePath) { $TemplatePath = Join-Path $infraRoot 'main.bicep' }
if (-not $ParametersPath) { $ParametersPath = Join-Path $infraRoot 'main.parameters.json' }

$euRegions = @('francecentral', 'germanywestcentral', 'italynorth', 'spaincentral', 'swedencentral', 'westeurope',
    'northeurope', 'polandcentral', 'austriaeast', 'belgiumcentral', 'denmarkeast', 'finlandcentral', 'greececentral')

# Only these built-in role definitions may appear in the template.
$approvedRoleIds = @{
    '53ca6127-db72-4b80-b1b0-d745d6d5456d' = 'Foundry User'
    'eed3b665-ab3a-47b6-8f48-c9382fb1dad6' = 'Foundry Agent Consumer'
    'ba92f5b4-2d11-453d-a403-e96b0029c9fe' = 'Storage Blob Data Contributor'
    '3913510d-42f4-4e42-8a64-420c390055eb' = 'Monitoring Metrics Publisher'
    '7f951dda-4ed3-4680-a7ca-43fe172d538d' = 'AcrPull'
}

$failures = New-Object System.Collections.Generic.List[string]
$checks = 0
function Assert-That([bool] $condition, [string] $message) {
    $script:checks++
    if (-not $condition) { $script:failures.Add($message) }
}

function Invoke-Bicep([string[]] $arguments) {
    $bicep = Get-Command bicep -ErrorAction SilentlyContinue
    if (-not $bicep -and $env:USERPROFILE) {
        $candidate = Join-Path $env:USERPROFILE '.azure\bin\bicep.exe'
        if (Test-Path $candidate) { $bicep = $candidate }
    }
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($bicep) { $output = & $bicep @arguments 2>&1 }
        else { $output = & az bicep @arguments 2>&1 }
        return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = @($output | ForEach-Object { "$_" }) }
    }
    finally { $ErrorActionPreference = $previous }
}

# Walks the compiled template, including nested module templates, and yields every resource declaration.
function Get-TemplateResources($template) {
    if ($null -eq $template -or -not ($template.PSObject.Properties.Name -contains 'resources')) { return }
    $resources = $template.resources
    $items = if ($resources -is [System.Array]) { $resources } else { $resources.PSObject.Properties | ForEach-Object { $_.Value } }
    foreach ($resource in $items) {
        if ($resource.PSObject.Properties.Name -contains 'existing' -and $resource.existing) { continue }
        $resource
        if ($resource.type -eq 'Microsoft.Resources/deployments') {
            Get-TemplateResources $resource.properties.template
        }
    }
}

$TemplatePath = (Resolve-Path $TemplatePath).Path
$compiledPath = Join-Path ([System.IO.Path]::GetTempPath()) ("main-{0}.json" -f [guid]::NewGuid().ToString('N'))

try {
    $build = Invoke-Bicep @('build', $TemplatePath, '--outfile', $compiledPath)
    Assert-That ($build.ExitCode -eq 0) "bicep build failed (exit $($build.ExitCode)):`n$($build.Output -join "`n")"
    $lint = Invoke-Bicep @('lint', $TemplatePath)
    $lintErrors = @($lint.Output | Where-Object { $_ -match ' : Error ' })
    $lintWarnings = @($lint.Output | Where-Object { $_ -match ' : Warning ' })
    Assert-That ($lint.ExitCode -eq 0 -and $lintErrors.Count -eq 0) "bicep lint reported errors (exit $($lint.ExitCode)):`n$($lint.Output -join "`n")"
    Assert-That ($lintWarnings.Count -eq 0) "bicep lint reported warnings:`n$($lintWarnings -join "`n")"
    if ($build.ExitCode -ne 0) {
        Write-Host "FAILED: bicep build did not produce a template" -ForegroundColor Red
        $failures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
        exit 1
    }

    $text = [System.IO.File]::ReadAllText($compiledPath)
    $template = $text | ConvertFrom-Json
    $resources = @(Get-TemplateResources $template)
    Assert-That ($resources.Count -gt 0) 'Compiled template contains no resources.'

    function Get-OfType([string] $type) { @($resources | Where-Object { $_.type -eq $type }) }

    $expectedTypes = @(
        'Microsoft.CognitiveServices/accounts', 'Microsoft.CognitiveServices/accounts/projects',
        'Microsoft.CognitiveServices/accounts/deployments', 'Microsoft.Storage/storageAccounts',
        'Microsoft.Insights/components', 'Microsoft.OperationalInsights/workspaces',
        'Microsoft.App/managedEnvironments', 'Microsoft.ManagedIdentity/userAssignedIdentities',
        'Microsoft.ContainerRegistry/registries', 'Microsoft.Authorization/roleAssignments')
    foreach ($type in $expectedTypes) {
        Assert-That (@(Get-OfType $type).Count -gt 0) "Expected at least one $type resource."
    }

    foreach ($r in Get-OfType 'Microsoft.CognitiveServices/accounts') {
        Assert-That ($r.kind -eq 'AIServices') "Foundry resource must be kind AIServices, found '$($r.kind)'."
        Assert-That ($r.properties.disableLocalAuth -eq $true) 'Foundry resource must set disableLocalAuth: true.'
        Assert-That ($r.properties.allowProjectManagement -eq $true) 'Foundry resource must set allowProjectManagement: true.'
    }
    foreach ($r in Get-OfType 'Microsoft.Storage/storageAccounts') {
        Assert-That ($r.properties.allowSharedKeyAccess -eq $false) 'Storage must set allowSharedKeyAccess: false.'
        Assert-That ($r.properties.allowBlobPublicAccess -eq $false) 'Storage must set allowBlobPublicAccess: false.'
    }
    foreach ($r in Get-OfType 'Microsoft.Insights/components') {
        Assert-That ($r.properties.DisableLocalAuth -eq $true) 'Application Insights must set DisableLocalAuth: true.'
    }
    foreach ($r in Get-OfType 'Microsoft.OperationalInsights/workspaces') {
        Assert-That ($r.properties.features.disableLocalAuth -eq $true) 'Log Analytics must set features.disableLocalAuth: true.'
    }
    foreach ($r in Get-OfType 'Microsoft.ContainerRegistry/registries') {
        Assert-That ($r.properties.adminUserEnabled -eq $false) 'Container registry must set adminUserEnabled: false.'
    }
    foreach ($r in Get-OfType 'Microsoft.App/managedEnvironments') {
        Assert-That ($r.properties.appLogsConfiguration.destination -ne 'log-analytics') 'Container Apps logs must not use the Log Analytics shared key.'
    }

    foreach ($function in 'listKeys', 'listCredentials', 'listAdminKeys', 'listConnectionStrings') {
        Assert-That ($text -notmatch "$function\(") "Template must not call $function()."
    }
    Assert-That ($text -notmatch 'ConnectionString') 'Template must not reference connection strings.'

    foreach ($output in $template.outputs.PSObject.Properties) {
        Assert-That ($output.Name -notmatch '(?i)key|secret|password|connectionstring|token|sas') "Output '$($output.Name)' looks like a secret."
    }

    $locations = @($template.parameters.location.allowedValues)
    Assert-That ($locations.Count -gt 0) 'location must restrict allowedValues.'
    foreach ($location in $locations) {
        Assert-That ($euRegions -contains $location) "location allows non-EU region '$location'."
    }
    $modelSkus = @($template.parameters.modelSkuName.allowedValues)
    Assert-That ($modelSkus.Count -gt 0 -and -not ($modelSkus | Where-Object { $_ -match '^Global' })) 'modelSkuName must not allow Global deployment types (inference outside the EU).'

    $guidPattern = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'
    $templateGuids = @([regex]::Matches($text, $guidPattern) | ForEach-Object { $_.Value.ToLowerInvariant() } | Sort-Object -Unique)
    foreach ($id in $templateGuids) {
        Assert-That ($approvedRoleIds.ContainsKey($id)) "Template contains GUID '$id', which is not an approved role definition."
    }
    Assert-That ($templateGuids.Count -eq $approvedRoleIds.Count) "Expected $($approvedRoleIds.Count) role definition IDs, found $($templateGuids.Count)."

    $parametersText = [System.IO.File]::ReadAllText((Resolve-Path $ParametersPath).Path)
    Assert-That ($parametersText -notmatch $guidPattern) 'main.parameters.json must not contain IDs; use azd environment variables.'
    $parameterNames = @(($parametersText | ConvertFrom-Json).parameters.PSObject.Properties.Name)
    foreach ($name in $parameterNames) {
        Assert-That ($template.parameters.PSObject.Properties.Name -contains $name) "main.parameters.json sets unknown parameter '$name'."
    }

    $roleAssignments = @(Get-OfType 'Microsoft.Authorization/roleAssignments').Count
    Write-Host ("Resources: {0}; role assignment declarations: {1}; role IDs: {2}; checks: {3}" -f $resources.Count, $roleAssignments, $templateGuids.Count, $checks)
}
finally {
    if (Test-Path $compiledPath) { Remove-Item $compiledPath -Force }
}

if ($failures.Count -gt 0) {
    Write-Host "FAILED: $($failures.Count) of $checks checks" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "PASSED: $checks checks" -ForegroundColor Green
exit 0
