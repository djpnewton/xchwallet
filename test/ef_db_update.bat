set DB_TYPE=sqlite
set CONNECTION_STRING=Data Source=wallet.db
dotnet ef database update --project ..\xchwallet\xchwallet.csproj
