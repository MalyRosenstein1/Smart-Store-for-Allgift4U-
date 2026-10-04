param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$MasterDatabase = "master"
)

$ErrorActionPreference = "Stop"

$services = Get-Service | Where-Object { $_.DisplayName -like "SQL Server*" }
Write-Host "SQL Server services found:" -ForegroundColor Cyan
$services | Select-Object Name, DisplayName, Status, StartType | Format-Table -AutoSize

$service = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne "Running") {
    Write-Host "Starting SQL Server (SQLEXPRESS)..." -ForegroundColor Yellow
    Start-Service -Name "MSSQL`$SQLEXPRESS"
}

$connectionString = "Server=$Server;Database=$MasterDatabase;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)

try {
    $connection.Open()
    Write-Host "Connected to SQL Server instance: $Server" -ForegroundColor Green
}
finally {
    $connection.Close()
}
