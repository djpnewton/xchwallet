set DATA_SOURCE=%1

set DB_TYPE=sqlite
set CONNECTION_STRING=Data Source=%DATA_SOURCE%
dotnet ef database update --project ..\xchwallet\xchwallet.csproj --context FiatWalletContext
