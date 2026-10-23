# InfoPresupuestal · BackEnd

API web en **.NET 9** (Minimal APIs) organizada con **Vertical Slice Architecture**, con **PostgreSQL** vía Entity Framework Core 9 y **Redis** como caché distribuida.

## Comandos

```bash
docker compose up -d                            # PostgreSQL 17 + Redis 7
dotnet tool restore                             # dotnet-ef
dotnet run --project src/InfoPresupuestal.Api   # http://localhost:5080 (aplica migraciones en Development)
dotnet test
```

- OpenAPI en `http://localhost:5080/openapi/v1.json` (solo Development).
- `src/InfoPresupuestal.Api/InfoPresupuestal.Api.http` tiene solicitudes de ejemplo.
- Cadenas de conexión: `ConnectionStrings:Postgres` y `ConnectionStrings:Redis`. Fuera de Development van por variables de entorno (`ConnectionStrings__Postgres`) o user-secrets, nunca en `appsettings.json`.
- Nueva migración: `dotnet ef migrations add <Nombre> -p src/InfoPresupuestal.Api -o Common/Persistence/Migrations`.

## Arquitectura

```
BackEndInfoPresupuestal.sln
├── Directory.Build.props / Directory.Packages.props   # net9.0, nullable, warnings = errores, versiones centralizadas
├── docker-compose.yml
├── src/InfoPresupuestal.Api/
│   ├── Program.cs                     # composición: registra Common y descubre los slices
│   ├── Common/                        # infraestructura transversal, sin lógica de negocio
│   │   ├── Abstractions/
│   │   │   ├── Messaging.cs                 # ICommand, IQuery, ICachedQuery, ICommandHandler, IQueryHandler
│   │   │   ├── Result.cs / Error.cs         # errores esperados sin excepciones
│   │   │   └── HandlerRegistration.cs       # registra handlers por reflexión (+ decorador de caché)
│   │   ├── Caching/
│   │   │   ├── ICacheService.cs             # abstracción que usan los slices
│   │   │   ├── DistributedCacheService.cs   # IDistributedCache + System.Text.Json
│   │   │   ├── CachedQueryHandler.cs        # decorador cache-aside para ICachedQuery
│   │   │   └── CachingServiceCollectionExtensions.cs   # AddStackExchangeRedisCache
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs              # aplica las IEntityTypeConfiguration de cada feature
│   │   │   ├── PersistenceServiceCollectionExtensions.cs  # UseNpgsql + reintentos
│   │   │   └── Migrations/
│   │   ├── Endpoints/                       # IEndpoint, descubrimiento y Result → ProblemDetails
│   │   ├── Validation/ValidationFilter.cs   # filtro de endpoint con FluentValidation
│   │   └── Errors/GlobalExceptionHandler.cs
│   └── Features/
│       └── Products/
│           ├── Product.cs                   # entidad (fábrica Create, setters privados)
│           ├── ProductConfiguration.cs      # mapeo EF Core de la tabla products
│           ├── ProductErrors.cs             # errores de negocio de la feature
│           ├── ProductsApi.cs               # ruta, tag y claves de caché de la feature
│           ├── CreateProduct/               # comando · POST /api/products
│           │   ├── CreateProductCommand.cs
│           │   ├── CreateProductValidator.cs
│           │   ├── CreateProductHandler.cs
│           │   ├── CreateProductMapping.cs
│           │   ├── CreateProductResponse.cs
│           │   └── CreateProductEndpoint.cs
│           └── GetProductById/              # consulta · GET /api/products/{id}
│               ├── GetProductByIdQuery.cs       # ICachedQuery: clave y expiración
│               ├── GetProductByIdHandler.cs     # solo lee PostgreSQL
│               ├── GetProductByIdMapping.cs     # proyección Expression → SQL
│               ├── GetProductByIdResponse.cs
│               └── GetProductByIdEndpoint.cs
└── tests/InfoPresupuestal.Api.Tests/     # xUnit: handlers (EF InMemory) y decorador de caché
```

### Reglas

1. **Un slice es una carpeta por caso de uso.** Contiene su solicitud (Command o Query), validador, handler, mapeo, respuesta y endpoint. Cambiar un caso de uso toca una sola carpeta.
2. **Un slice no importa de otro slice.** Lo que comparten los slices de una feature (entidad, configuración EF, errores, claves de caché) vive en la raíz de la feature. La única excepción es el nombre de ruta `GetProductByIdEndpoint.Name`, que `CreateProduct` usa en `CreatedAtRoute`.
3. **Una feature no importa de otra feature.** Si dos features necesitan lo mismo y es infraestructura, va a `Common/`.
4. **`Common/` no conoce el negocio.** Nada en `Common/` importa de `Features/`. `AppDbContext` no declara `DbSet` por feature: los handlers usan `db.Set<Product>()`.
5. **Agregar un slice no modifica código existente (OCP).** Handlers, validadores y endpoints se descubren por reflexión; `Program.cs` no cambia.
6. **Interfaces pequeñas (ISP).** Cada endpoint inyecta solo su `ICommandHandler<TCommand, TResponse>` o `IQueryHandler<TQuery, TResponse>`; no hay mediador genérico.
7. **La caché es una preocupación transversal (SRP).** Una consulta que implementa `ICachedQuery` se cachea en Redis con el decorador `CachedQueryHandler`; el handler solo habla con PostgreSQL. Si Redis no responde, se registra un warning y se lee de la base de datos.
8. **Los slices dependen de abstracciones (DIP):** `ICacheService` en lugar de Redis, `TimeProvider` en lugar de `DateTime.UtcNow`.
9. **Errores esperados con `Result<T>`**, convertidos a ProblemDetails (400, 404, 409). Las excepciones quedan para lo inesperado y las atrapa `GlobalExceptionHandler`.
10. **La validación ocurre antes del handler**, en `ValidationFilter<T>`; la entidad además protege sus invariantes en `Product.Create`.
11. **Consultas con `AsNoTracking` y proyección**, para leer solo las columnas de la respuesta.

### Flujo de `GET /api/products/{id}`

```
GetProductByIdEndpoint
  → IQueryHandler<GetProductByIdQuery, …>   (resuelto como CachedQueryHandler)
      ├─ Redis: infopresupuestal:products:{id} → si existe, responde
      └─ GetProductByIdHandler → PostgreSQL → guarda en Redis 10 min → responde
```

### Agregar un slice

1. Crear `Features/<Feature>/<CasoDeUso>/` con `…Command` o `…Query`, `…Handler`, `…Response`, `…Endpoint` (y `…Validator` / `…Mapping` si aplica).
2. Si es una consulta cacheable, implementar `ICachedQuery<TResponse>`.
3. Si hay entidades nuevas, agregar su `IEntityTypeConfiguration` y una migración.

## Decisiones

- **Sin MediatR**: desde la v13 tiene licencia comercial, y para este tamaño basta con inyectar el handler directamente.
- **SDK**: `global.json` pide .NET 9 y permite un SDK más nuevo (`rollForward: latestMajor`); el proyecto apunta a `net9.0`.
- **Ids `Guid.CreateVersion7`**: ordenables por fecha, buenos para índices en PostgreSQL.
