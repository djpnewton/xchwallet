set DATA_SOURCE=%1
set MIGRATION_NAME=%2

set DB_TYPE=sqlite
set CONNECTION_STRING=Data Source=%DATA_SOURCE%
dotnet ef migrations add %MIGRATION_NAME% --project ..\xchwallet\xchwallet.csproj --context WalletContext
