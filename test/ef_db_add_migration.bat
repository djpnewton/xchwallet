set DB_TYPE=sqlite
set CONNECTION_STRING=Data Source=wallet.db
dotnet ef migrations add %1 --project ..\xchwallet\xchwallet.csproj
