param(
    [string]$Image = 'mcr.microsoft.com/mssql/server:2022-latest',
    [string]$ToolPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$containerName = 'dbmapper-test-' + [guid]::NewGuid().ToString('N')
$previousPassword = $env:MSSQL_SA_PASSWORD
$previousConnection = $env:DBMAPPER_TEST_ADMIN
$previousTool = $env:DBMAPPER_TEST_TOOL
try {
    $env:DBMAPPER_TEST_TOOL = if ($ToolPath) { (Resolve-Path -LiteralPath $ToolPath).Path } else { $null }
    $env:MSSQL_SA_PASSWORD = 'Test!aA1' + [guid]::NewGuid().ToString('N')
    Write-Host 'Starting a disposable SQL Server fixture on loopback...'
    docker run --detach --rm --name $containerName --publish '127.0.0.1::1433' --env 'ACCEPT_EULA=Y' --env 'MSSQL_PID=Developer' --env MSSQL_SA_PASSWORD $Image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the disposable database container.' }
    $portMapping = docker port $containerName '1433/tcp'
    if ($LASTEXITCODE -ne 0 -or $portMapping -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Could not resolve the loopback fixture port.' }
    $portNumber = $Matches[1]
    $env:DBMAPPER_TEST_ADMIN = "Server=127.0.0.1,$portNumber;Database=master;User ID=sa;Password=$env:MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True;Connect Timeout=2;ConnectRetryCount=0;Pooling=False"
    dotnet run --project (Join-Path $projectRoot 'tests/DbMapper.SelfTest') -c Release -- --integration
    if ($LASTEXITCODE -ne 0) { throw 'Database integration checks failed. Credential details were withheld.' }
    if ($IsWindows -and $env:DBMAPPER_TEST_TOOL) {
        $scratch = Join-Path ([IO.Path]::GetTempPath()) ('dbmapper-pathcheck-' + [guid]::NewGuid().ToString('N'))
        $outside = Join-Path $scratch 'outside'
        $bundle = Join-Path $scratch 'bundle'
        $directLink = Join-Path $scratch 'linked-output'
        $childLink = Join-Path $bundle 'linked-child'
        try {
            New-Item -ItemType Directory -Path $outside, $bundle -Force | Out-Null
            [IO.File]::WriteAllText((Join-Path $outside 'keep.md'), 'Handwritten content')
            New-Item -ItemType Junction -Path $directLink -Target $outside | Out-Null
            New-Item -ItemType Junction -Path $childLink -Target $outside | Out-Null
            foreach ($candidate in @($directLink, (Join-Path $directLink 'nested'), $bundle)) {
                $startInfo = [Diagnostics.ProcessStartInfo]::new($env:DBMAPPER_TEST_TOOL)
                $startInfo.UseShellExecute = $false
                $startInfo.CreateNoWindow = $true
                $startInfo.RedirectStandardOutput = $true
                $startInfo.RedirectStandardError = $true
                $startInfo.ArgumentList.Add('--output')
                $startInfo.ArgumentList.Add($candidate)
                $process = [Diagnostics.Process]::Start($startInfo)
                $standardOutput = $process.StandardOutput.ReadToEndAsync()
                $standardError = $process.StandardError.ReadToEndAsync()
                $process.WaitForExit()
                $diagnostic = $standardOutput.Result + $standardError.Result
                $exitCode = $process.ExitCode
                $process.Dispose()
                if ($exitCode -ne 2 -or $diagnostic -notmatch 'symbolic links or junctions') { throw 'Output junction protection check failed.' }
            }
            if ([IO.File]::ReadAllText((Join-Path $outside 'keep.md')) -ne 'Handwritten content') { throw 'Junction target was modified.' }
            Write-Host 'PASS: 3 Windows junction guards; target content preserved.'
        }
        finally {
            # Delete junction entries non-recursively before deleting the isolated scratch tree.
            foreach ($link in @($directLink, $childLink)) {
                if ([IO.Directory]::Exists($link)) { [IO.Directory]::Delete($link, $false) }
            }
            $resolvedScratch = [IO.Path]::GetFullPath($scratch)
            $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
            if ($resolvedScratch.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolvedScratch).StartsWith('dbmapper-pathcheck-')) {
                Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
            }
        }
    }
}
finally {
    # The exact randomly named container belongs to this invocation; no shared service is touched.
    docker rm --force $containerName 2>$null | Out-Null
    $env:MSSQL_SA_PASSWORD = $previousPassword
    $env:DBMAPPER_TEST_ADMIN = $previousConnection
    $env:DBMAPPER_TEST_TOOL = $previousTool
}
