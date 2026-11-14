param(
    [string]$Executable = (Join-Path $PSScriptRoot '../Builds/Z1/AnseongZ1.exe'),
    [string]$Results = (Join-Path $PSScriptRoot '../docs/validation/z1')
)
$ErrorActionPreference = 'Stop'
$Executable = [IO.Path]::GetFullPath($Executable)
$Results = [IO.Path]::GetFullPath($Results)
if (-not (Test-Path -LiteralPath $Executable)) { throw 'Build the Windows lab first: Anseong/Z1/Build Windows Lab.' }
New-Item -ItemType Directory -Path $Results -Force | Out-Null
foreach ($variant in @(3,4)) {
    $hostSeat = if ($variant -eq 3) { 'mech' } else { 'support' }
    $clientSeat = if ($variant -eq 3) { 'support' } else { 'mech' }
    $networkPort = 7780 + $variant
    $hostRun = $null; $clientRun = $null
    try {
        $hostRun = Start-Process -FilePath $Executable -ArgumentList "-batchmode -nographics --z1-auto --host true --seat $hostSeat --variant $variant --port $networkPort --result `"$Results\v$variant-host.json`" -logFile `"$Results\v$variant-host.log`"" -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
        $clientRun = Start-Process -FilePath $Executable -ArgumentList "-batchmode -nographics --z1-auto --host false --seat $clientSeat --variant $variant --port $networkPort --result `"$Results\v$variant-client.json`" -logFile `"$Results\v$variant-client.log`"" -WindowStyle Hidden -PassThru
        if (-not $hostRun.WaitForExit(55000)) { throw "Variant $variant host timeout" }
        if (-not $clientRun.WaitForExit(55000)) { throw "Variant $variant client timeout" }
        if ($hostRun.ExitCode -ne 0 -or $clientRun.ExitCode -ne 0) { throw "Variant $variant process failed" }
        foreach ($role in @('host','client')) {
            $state = Get-Content -LiteralPath "$Results\v$variant-$role.json" -Raw | ConvertFrom-Json
            if ($state.phase -ne 3 -or $state.travel -ne 1 -or $state.releases -ne 1) { throw "Variant $variant $role state mismatch" }
            if (Select-String -LiteralPath "$Results\v$variant-$role.log" -Pattern 'Exception:' -Quiet) { throw "Variant $variant $role exception" }
        }
        Write-Output "PASS: variant $variant, host=$hostSeat, client=$clientSeat, single release, exit clear."
    }
    finally {
        # Stop only the processes this invocation created, if a test timed out.
        foreach ($process in @($hostRun,$clientRun)) {
            if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        }
    }
}
