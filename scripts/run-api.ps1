$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5246'
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) {
    $env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5433;Database=insulin_coffee;Username=postgres;Password=postgres'
}
if ([string]::IsNullOrWhiteSpace($env:Jwt__SigningKey)) {
    Write-Host 'Using Jwt:SigningKey from .NET user-secrets. Run the setup command in README.md if it is missing.'
}
& 'C:\Program Files\dotnet\dotnet.exe' "$PSScriptRoot\..\backend\src\InsulinAndCoffee.Api\bin\Debug\net9.0\InsulinAndCoffee.Api.dll"
