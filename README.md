# Brar · Plataforma de Natural (.NET 10)

Microservicio del **superadmin de plataforma** de Natural: crear y activar/desactivar **tenants** (empresas), administrar sus **accesos** y dejar **auditoría** de todo. Tiene **BD propia** (PostgreSQL) y **no toca la BD de Natural**: todo cambio sobre empresas y usuarios lo hace llamando a la API interna `api/Plataforma/*` de Natural con una clave de servicio.

> Plan de trabajo: `../workplan_plataforma.md`. Estado: fases F1–F4 (esqueleto, seguridad, persistencia, empresas).

## Cómo funciona

```
Front (rol 00) ──JWT de Natural──▶ Brar ──X-Service-Key + X-Actor──▶ Natural (api/Plataforma/*)
                                     └──▶ BD propia: Auditoria, SesionSoporte
```

- **No emite tokens.** El superadmin inicia sesión en Natural; Brar valida ese JWT (firma, issuer, audience, expiración) y exige el **rol `00`**. Otros roles → 403; sin token → 401.
- Cada acción que cambia datos se registra en `Auditoria` **antes** de llamar a Natural (`pendiente`) y se cierra en `ok`/`error`. Si no se puede escribir la auditoría, la acción no se ejecuta.
- Natural caído o mal configurado → 502; sin respuesta a tiempo → 504.

## Estructura

```
Brar.Domain          Auditoria, SesionSoporte (sin dependencias)
Brar.Application     casos de uso + puertos (INaturalPlataforma, IAuditoriaRepositorio)
Brar.Infrastructure  EF Core/Npgsql (migraciones) + cliente HTTP de Natural
Brar                 API: controllers, JWT de Natural, config obligatoria, Program.cs
Brar.Tests           pruebas (dominio, casos de uso, cliente HTTP, seguridad de la API)
```

## Endpoints (todos exigen rol `00`, salvo `/health`)

| Método y ruta | Qué hace |
|---|---|
| `GET /health` | Estado del servicio (público) |
| `GET /Empresas` | Lista empresas con conteos de personas/accesos |
| `POST /Empresas` | Crea empresa + primer administrador (contraseña = SHA-256 base64, 44 car.) |
| `PUT /Empresas/{id}` | Actualiza datos de la empresa |
| `PATCH /Empresas/{id}/Estado?activa=` | Activa/desactiva la empresa |

Errores: `ProblemDetails` con el mensaje en `title`/`detail`.

## Configuración

Ningún secreto vive en el repositorio. El servicio **no arranca** si falta una obligatoria y el error dice cuál. Plantilla: [`.env.example`](.env.example).

| Variable | Obligatoria | Para qué |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | sí | BD propia de Brar (proyecto **nuevo** de Supabase en producción) |
| `NaturalJwt__Key` | sí | La **misma** clave con la que Natural firma sus JWT (≥ 32 car.) |
| `NaturalJwt__Issuer` / `__Audience` | no | Por defecto `Natural.Api` / `Natural.Client` |
| `NaturalApi__BaseUrl` | sí | URL base de Natural, p. ej. `https://naturalback.vip/v2/api` |
| `NaturalApi__ClaveServicio` | sí | Igual a `Plataforma__ClaveServicio` de Natural (≥ 32 car.) |
| `NaturalApi__TimeoutSegundos` | no | Timeout por llamada a Natural (15) |
| `Cors__AllowedOrigins__0…` | no | Orígenes permitidos; vacío = cualquiera |
| `RunMigrationsOnStartup` | no | `true` aplica migraciones al arrancar |

En Natural hay que definir `Plataforma__ClaveServicio` con **el mismo valor** (vacía = su API de plataforma queda deshabilitada).

## Ejecutar en local (Docker)

```bash
cp .env.example .env        # rellena POSTGRES_PASSWORD, NaturalJwt__Key, NaturalApi__*
docker compose up --build   # Postgres local (puerto 5433) + API en http://localhost:8080
curl http://localhost:8080/health
```

## Desplegar por imagen

```bash
docker build -t brar-api .
# subir a tu registro:  docker tag brar-api <registro>/brar-api:<version> && docker push <registro>/brar-api:<version>
docker run -p 8080:8080 --env-file .env -e RunMigrationsOnStartup=true brar-api   # 1.er arranque
```

En producción no uses el Postgres del compose: apunta `ConnectionStrings__DefaultConnection` a la BD de Supabase de Brar y deja `RunMigrationsOnStartup=true` solo en el primer arranque. La imagen escucha en `8080`, corre sin privilegios y no incluye secretos.

## Desarrollo

```bash
dotnet test Brar.slnx
dotnet user-secrets --project Brar set "NaturalJwt:Key" "..."     # y las demás claves con ':'
dotnet ef migrations add <Nombre> --project Brar.Infrastructure --startup-project Brar --output-dir Migrations
```

Convenciones: código y rutas en español (como Natural y Citas); nunca loguear contraseñas, tokens ni la clave de servicio.
