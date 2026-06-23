$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5246'
& 'C:\Program Files\dotnet\dotnet.exe' "$PSScriptRoot\..\backend\src\InsulinAndCoffee.Api\bin\Debug\net9.0\InsulinAndCoffee.Api.dll"
