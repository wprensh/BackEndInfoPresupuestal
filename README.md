# InfoPresupuestal · BackEnd

API web en **.NET 9** (Minimal APIs) organizada con **Vertical Slice Architecture**, **multi-tenant**, con **PostgreSQL** vía Entity Framework Core 9 y **Redis** como caché distribuida.

## Comandos

```bash
docker compose up -d                            # PostgreSQL 17 + Redis 7
dotnet tool restore                             # dotnet-ef
dotnet run --project src/InfoPresupuestal.Api   # http://localhost:5080 (aplica migraciones en Development)
dotnet test
```

- OpenAPI en `http://localhost:5080/openapi/v1.json` (solo Development).
- `src/InfoPresupuestal.Api/InfoPresupuestal.Api.http` tiene solicitudes de ejemplo.
- Antes de arrancar, instala el catálogo de insumos con `database/instalar.sql` (ver [database/README.md](database/README.md)): el mantenimiento de tablas trabaja sobre el esquema `presupuesto`.
- Toda solicitud a `/api/*`, salvo el login, lleva `Authorization: Bearer <token>` de `POST /api/auth/login`. La empresa (tenant) sale del token.
- En Development, al arrancar se aplican las migraciones y, si no hay empresas, se siembran (`Seed` en `appsettings.Development.json`, contraseña `Seed:Password`):

  | Empresa | Usuario | Rol |
  |---|---|---|
  | `sistema` | `superadmin@infopresupuestal.local` | Superadministrador de la plataforma |
  | `demo` | `admin@demo.local` | Administrador |
  | `acme` | `admin@acme.local` | Administrador |

- Cadenas de conexión: `ConnectionStrings:Postgres` y `ConnectionStrings:Redis`. Fuera de Development van por variables de entorno (`ConnectionStrings__Postgres`) o user-secrets, nunca en `appsettings.json`. Lo mismo para `Jwt__SigningKey` (32 caracteres o más): sin ella la API no arranca.
- CORS: orígenes permitidos en `Cors:Origins`.
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
│   │   ├── Security/                        # JWT, permisos por módulo y acción, políticas
│   │   │   ├── Modulos.cs                   # catálogo de módulos y acciones Ver/Insertar/Editar/Eliminar
│   │   │   ├── PermissionEvaluator.cs       # única regla de autorización
│   │   │   ├── PermissionAuthorization.cs   # .RequirePermission(modulo, accion) en cada endpoint
│   │   │   └── TokenService.cs              # emite el JWT con tenant, rol y permisos
│   │   ├── MultiTenancy/
│   │   │   ├── ITenantContext.cs / TenantContext.cs   # tenant de la solicitud (scoped)
│   │   │   ├── ITenantEntity.cs             # marca entidades con TenantId
│   │   │   ├── TenantResolutionFilter.cs    # toma el tenant del JWT y lo valida contra ITenantStore
│   │   │   ├── ITenantStore.cs              # empresas activas (lo implementa Administracion/Empresas)
│   │   │   ├── TenantSaveChangesInterceptor.cs   # asigna TenantId al insertar, bloquea escrituras cruzadas
│   │   │   ├── TenantCacheService.cs        # antepone tenant:{id}: a cada clave de caché
│   │   │   └── TenantOpenApiTransformer.cs  # documenta el header en OpenAPI
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs              # configuraciones de cada feature + filtro global por tenant
│   │   │   ├── PersistenceServiceCollectionExtensions.cs  # UseNpgsql + reintentos
│   │   │   └── Migrations/
│   │   ├── Endpoints/                       # IEndpoint, descubrimiento y Result → ProblemDetails
│   │   ├── Validation/ValidationFilter.cs   # filtro de endpoint con FluentValidation
│   │   └── Errors/GlobalExceptionHandler.cs
│   └── Features/
│       ├── Administracion/              # esquema seguridad (migración EF Core)
│       │   ├── Autenticacion/           # Login (público, con límite de intentos) y Me
│       │   ├── Empresas/                # tenants: alta con roles y administrador, siembra
│       │   ├── Roles/                   # roles por empresa con su matriz de permisos
│       │   └── Usuarios/                # usuarios por empresa, contraseñas con hash PBKDF2
│       ├── Mantenimiento/               # esquema presupuesto (tablas de database/, sin migración)
│       │   ├── UnidadesMedida/ · TiposInsumo/   # catálogos globales
│       │   ├── Clasificaciones/
│       │   ├── Insumos/                 # código MAT-0001 automático, precio vigente
│       │   └── PreciosInsumo/           # historial de precios
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
12. **El aislamiento por tenant es automático y falla cerrado.** Ningún slice lee ni escribe `TenantId`:
    - el grupo de endpoints exige un tenant válido (`RequireTenant`) antes de validar o ejecutar el handler;
    - toda entidad `ITenantEntity` recibe la columna `tenant_id`, un índice y un filtro global de EF Core;
    - el interceptor asigna el tenant al insertar y rechaza modificar o borrar filas de otro tenant;
    - `ICacheService` ya llega aislado por tenant;
    - si algo intenta consultar sin tenant resuelto, `TenantContext` lanza una excepción en lugar de devolver datos.
13. **Las restricciones únicas incluyen el tenant** (por ejemplo `(tenant_id, name)` en `products`).
14. **`IgnoreQueryFilters()` solo en código administrativo explícito**, nunca en un slice de tenant (hoy: `Me` y revisar el uso de un catálogo global antes de borrarlo).
15. **Cada endpoint declara su permiso** con `.RequirePermission(Modulos.X, Accion.Y)`. Los endpoints quedan en uno de tres grupos según su interfaz: `IEndpoint` (autenticado + tenant), `IPlatformEndpoint` (autenticado, sin tenant) e `IPublicEndpoint` (anónimo, solo el login).
16. **En los slices de Administración y Mantenimiento un archivo es un caso de uso**: solicitud, validador, handler y endpoint juntos. Products conserva un archivo por clase como ejemplo de la forma extendida.

## Multi-tenant

Modelo: **una base compartida con columna `tenant_id`** en cada tabla de negocio. Cada tenant es una **empresa** (`seguridad.empresa`).

```
Solicitud con Authorization: Bearer <jwt de acme>
  → JwtBearer valida firma y vigencia (401 si no)
  → TenantResolutionFilter: claim tenant = acme, empresa activa → TenantContext = acme
      (sin claim → 401 Tenant.Missing · empresa inactiva → 403 Tenant.Inactive)
      el superadministrador puede enviar X-Tenant-Id para actuar sobre otra empresa
  → política perm:modulo:accion (403 si el rol no tiene el permiso)
  → ValidationFilter → Handler
      ├─ EF Core: WHERE tenant_id = 'acme' en cada consulta; INSERT con tenant_id = 'acme'
      └─ Redis: infopresupuestal:tenant:acme:products:{id}
```

Un tenant que necesite base propia se resuelve con una cadena de conexión por tenant sin cambiar los slices.

## Administración y control de acceso

- **Empresas** (`/api/admin/empresas`, solo superadministrador): crear una empresa crea en la misma transacción sus roles `Administrador` y `Consulta` y su primer usuario administrador. Eliminar una empresa la **desactiva**: sus usuarios dejan de poder entrar y sus tokens dejan de servir.
- **Usuarios** (`/api/admin/usuarios`): inician sesión con empresa + correo + contraseña. El mismo correo puede existir en dos empresas. Las contraseñas se guardan con el hash PBKDF2 de ASP.NET Core Identity (mínimo 8 caracteres con mayúscula, minúscula y número). Un usuario no puede desactivarse, cambiar su propio rol ni eliminarse.
- **Roles** (`/api/admin/roles`, `/api/admin/modulos`): cada rol tiene, por módulo, las acciones **Ver, Insertar, Editar y Eliminar**. Insertar, Editar o Eliminar implican Ver. Los roles del sistema no se editan ni se eliminan; un rol con usuarios no se elimina.
- **Permisos en el token**: el login devuelve el JWT con los permisos del rol (`perm: insumos:editar`). Un cambio de rol aplica en el siguiente inicio de sesión (el token dura `Jwt:ExpirationMinutes`).
- **Alcance de los módulos** (`Common/Security/Modulos.cs`):

  | Alcance | Módulos | Roles de empresa |
  |---|---|---|
  | Tenant | usuarios, roles, clasificaciones, insumos, precios-insumo, productos | Ver, Insertar, Editar, Eliminar |
  | Global | unidades-medida, tipos-insumo | Solo Ver; los cambia el superadministrador porque los comparten todas las empresas |
  | Plataforma | empresas | Ninguna; solo el superadministrador |

- **Login**: `POST /api/auth/login`, máximo 10 intentos por minuto por IP, y el mismo error 401 para empresa, correo o contraseña incorrectos.

## APU y presupuestos

Tablas en `database/scripts/05_apu_presupuesto.sql` (ver `database/README.md`). Módulos de permiso `apu` y `presupuestos`; al arrancar en Development, los roles de sistema de las empresas existentes los reciben (Administrador completo, Consulta solo ver).

| Endpoint | Qué hace |
|---|---|
| `GET/POST /api/apus`, `GET/PUT/DELETE /api/apus/{id}` | APU completo (encabezado + materiales, equipo y mano de obra). Código `APU-NNNN` automático. Un APU usado en presupuestos no se borra: se desactiva. |
| `POST /api/apus/{id}/duplicar` | Copia para ajustar sin tocar el original. |
| `GET/POST /api/presupuestos`, `GET/PUT/DELETE /api/presupuestos/{id}` | Presupuesto con capítulos e ítems. Un ítem con `apuId` toma del APU descripción, unidad y costo; los ítems se numeran 1.1, 1.2, 2.1 según su orden. Responde subtotales, participación por capítulo, AIU, IVA sobre utilidad, total y anticipo. |
| `PUT /api/presupuestos/{id}/estado` | BORRADOR, APROBADO o ANULADO. Solo un borrador se edita; uno aprobado no se borra. |
| `POST /api/presupuestos/{id}/recalcular` | Trae a los ítems el costo actual de sus APU (`itemsDesactualizados` dice cuántos cambiaron). |
| `POST /api/presupuestos/{id}/duplicar` | Copia como borrador nuevo. |

## Mantenimiento de tablas

CRUD con búsqueda y paginación (`?page=1&pageSize=20&search=texto`) sobre el esquema `presupuesto`:

| Ruta | Tabla | Notas |
|---|---|---|
| `/api/mantenimiento/unidades-medida` | `unidad_medida` | Global. No se borra si la usa algún insumo de cualquier empresa. |
| `/api/mantenimiento/tipos-insumo` | `tipo_insumo` | Global. El prefijo (MAT) no cambia después de creado. |
| `/api/mantenimiento/clasificaciones` | `clasificacion` | Por empresa. No se borra si tiene insumos; se puede desactivar. |
| `/api/mantenimiento/insumos` | `insumo` | Por empresa. Filtros `tipoInsumoId`, `clasificacionId`, `activo`. Devuelve el precio vigente. El código se asigna solo (MAT-3695) y el tipo no cambia. Eliminar borra también su historial. |
| `/api/mantenimiento/insumos/{id}/precios` | `insumo_precio` | Una fila por fecha de vigencia; moneda COP por defecto. |

Estas tablas las crean los scripts de `database/`, por eso EF Core las mapea con `ExcludeFromMigrations()`. Las tablas de `seguridad` sí van por migraciones.

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
3. Si hay entidades nuevas, implementar `ITenantEntity` (salvo que sean globales), agregar su `IEntityTypeConfiguration` y una migración.

## Decisiones

- **Sin MediatR**: desde la v13 tiene licencia comercial, y para este tamaño basta con inyectar el handler directamente.
- **SDK**: `global.json` pide .NET 9 y permite un SDK más nuevo (`rollForward: latestMajor`); el proyecto apunta a `net9.0`.
- **Ids `Guid.CreateVersion7`**: ordenables por fecha, buenos para índices en PostgreSQL.

## Base de datos de insumos

`database/` contiene el modelo PostgreSQL del catálogo de insumos (materiales, equipo y mano de obra) con su historial de precios, cargado desde el Excel de presupuestos. Está en los esquemas `presupuesto` y `carga`, separado de las tablas que maneja EF Core. Ver [database/README.md](database/README.md).

## Despliegue (Render + Neon)

La API se publica en Render como servicio **Docker** con el `Dockerfile` de la raíz (escucha en el puerto 8080, `ASPNETCORE_ENVIRONMENT=Production`). Variables de entorno:

| Variable | Valor |
|---|---|
| `ConnectionStrings__Postgres` | Cadena Npgsql de Neon, p. ej. `Host=<host>-pooler...neon.tech;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=Require` |
| `Jwt__SigningKey` | Clave aleatoria de 32+ caracteres |
| `Database__MigrateOnStartup` | `true`: aplica las migraciones EF (esquema `seguridad`) y la siembra al arrancar |
| `Seed__Enabled`, `Seed__Password`, `Seed__Empresas__0__*` | Siembra inicial (solo si no hay empresas) |
| `Cors__Origins__0`, `Cors__Origins__1` | Orígenes del frontend (GitHub Pages / Render) |

Sin `ConnectionStrings__Redis` la caché usa memoria del proceso. Las tablas de `presupuesto` se cargan con `database/instalar.sql` (ver `database/README.md`).
