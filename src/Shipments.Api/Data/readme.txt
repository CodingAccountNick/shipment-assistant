docker compose up --build -d      
dotnet ef migrations add InitialCreate --project src/Shipments.Api
dotnet ef database update --project src/Shipments.Api


dotnet ef database update --project src/Shipments.Api
docker compose exec db psql -U shipments -d shipments -c "\dt"

                 List of relations
 Schema |         Name          | Type  |   Owner   
--------+-----------------------+-------+-----------
 public | Shipments             | table | shipments
 public | __EFMigrationsHistory | table | shipments
(2 rows)