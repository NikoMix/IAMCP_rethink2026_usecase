<#
.SYNOPSIS
    Fails when TRX results show that no tests executed.

.DESCRIPTION
    `dotnet test` exits 0 when a filter matches nothing or a test project discovers no
    tests, so a green run can hide a suite that never ran. This script reads every TRX
    file under the results directory and fails when:
      - no TRX file exists,
      - any single TRX file reports zero executed tests (names the file), or
      - the executed total across all files is zero.

.PARAMETER ResultsDirectory
    Directory searched recursively for *.trx files.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ResultsDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ResultsDirectory -PathType Container)) {
    throw "Results directory '$ResultsDirectory' does not exist; no tests ran."
}

$trxFiles = @(Get-ChildItem -LiteralPath $ResultsDirectory -Recurse -File -Filter '*.trx')
if ($trxFiles.Count -eq 0) {
    throw "No TRX files found under '$ResultsDirectory'; no tests ran."
}

$ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
$totalExecuted = 0
$emptyFiles = [System.Collections.Generic.List[string]]::new()

foreach ($file in $trxFiles) {
    $counters = Select-Xml -LiteralPath $file.FullName -XPath '/t:TestRun/t:ResultSummary/t:Counters' -Namespace $ns
    if ($null -eq $counters) {
        throw "TRX file '$($file.FullName)' has no ResultSummary/Counters element."
    }

    $node = $counters.Node
    $executed = [int] $node.executed
    Write-Host ("{0}: total={1} executed={2} passed={3} failed={4}" -f `
            $file.Name, $node.total, $executed, $node.passed, $node.failed)

    if ($executed -eq 0) {
        $emptyFiles.Add($file.Name)
    }
    $totalExecuted += $executed
}

Write-Host "TRX files: $($trxFiles.Count); tests executed: $totalExecuted"

if ($totalExecuted -eq 0) {
    throw 'Zero tests executed across all TRX files.'
}
if ($emptyFiles.Count -gt 0) {
    throw "Zero tests executed in: $($emptyFiles -join ', ')"
}
