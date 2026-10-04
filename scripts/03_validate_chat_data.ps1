param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "SmartStore"
)

$ErrorActionPreference = "Stop"

$connectionString = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)

try {
    $connection.Open()
    Write-Host "Connected to database: $Database on $Server" -ForegroundColor Green

    $summaryQueries = @{
        "Products" = "SELECT COUNT(*) FROM dbo.Products"
        "Orders" = "SELECT COUNT(*) FROM dbo.OrderTemplates"
        "OrderComponents" = "SELECT COUNT(*) FROM dbo.OrderTemplateComponents"
        "MissingComponents" = "SELECT COUNT(*) FROM dbo.OrderTemplateComponents c LEFT JOIN dbo.Products p ON p.ProductID = c.ProductID WHERE p.ProductID IS NULL"
    }

    $missingComponents = 0

    foreach ($entry in $summaryQueries.GetEnumerator()) {
        $command = $connection.CreateCommand()
        $command.CommandText = $entry.Value
        $value = [int]$command.ExecuteScalar()
        if ($entry.Key -eq "MissingComponents") {
            $missingComponents = $value
        }
        Write-Host "$($entry.Key): $value" -ForegroundColor Cyan
    }

    Write-Host "`nProducts visible to chat stock updates:" -ForegroundColor Cyan
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT TOP (120) ProductName FROM dbo.Products ORDER BY ProductName"
    $reader = $command.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "- $($reader['ProductName'])"
    }
    $reader.Close()

    Write-Host "`nOrders visible to chat/image matching:" -ForegroundColor Cyan
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT TOP (120) OrderName FROM dbo.OrderTemplates ORDER BY OrderName"
    $reader = $command.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "- $($reader['OrderName'])"
    }
    $reader.Close()

    if ($missingComponents -eq 0) {
        Write-Host "`nValidation passed: every order component has a matching product." -ForegroundColor Green
    }
    else {
        Write-Host "`nValidation warning: $missingComponents order components do not have matching products." -ForegroundColor Red
    }
}
finally {
    $connection.Close()
}
