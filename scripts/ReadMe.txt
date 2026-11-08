grep -r "TargetFramework" --include=*.csproj .  ---lists all target frameworks

dotnet clean
dotnet build 2>&1 | tail -5
dotnet build 2>&1 | grep -c MSB3277

dotnet test

docker compose up -d --build
docker compose ps
docker compose logs shipments-api --tail 20

bash scripts/smoke-test.sh