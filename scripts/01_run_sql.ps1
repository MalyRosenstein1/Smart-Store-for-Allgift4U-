param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$MasterDatabase = "master",
    [string]$ScriptsRoot = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

$connectionString = "Server=$Server;Database=$MasterDatabase;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)

try {
    $connection.Open()
    Write-Host "Connected to SQL Server instance: $Server" -ForegroundColor Green

    $sqlFiles = Get-ChildItem -Path $ScriptsRoot -Filter "*.sql" | Sort-Object Name
    foreach ($file in $sqlFiles) {
        Write-Host "Running $($file.Name)..." -ForegroundColor Cyan
        $scriptText = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        $batches = [regex]::Split($scriptText, "^\s*GO\s*$", [System.Text.RegularExpressions.RegexOptions]::Multiline -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

        foreach ($batch in $batches) {
            $batch = $batch.Trim()
            if ([string]::IsNullOrWhiteSpace($batch)) { continue }

            $command = $connection.CreateCommand()
            $command.CommandText = $batch
            $command.ExecuteNonQuery() | Out-Null
        }

        Write-Host "Completed $($file.Name)" -ForegroundColor Green
    }

    Write-Host "SQL scripts completed successfully." -ForegroundColor Green
    Write-Host "Running chat data validation..." -ForegroundColor Cyan
    & (Join-Path $ScriptsRoot "03_validate_chat_data.ps1") -Server $Server -Database "SmartStore"
}
finally {
    $connection.Close()
}
