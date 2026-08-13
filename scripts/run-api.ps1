$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5246'
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) {
    $env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5433;Database=insulin_coffee;Username=postgres;Password=postgres'
}
& 'C:\Program Files\dotnet\dotnet.exe' "$PSScriptRoot\..\backend\src\InsulinAndCoffee.Api\bin\Debug\net9.0\InsulinAndCoffee.Api.dll"
