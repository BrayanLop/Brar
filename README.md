# Brar · Plataforma de Natural (.NET 10)

Microservicio del **superadmin de plataforma** de Natural: crear y activar/desactivar **tenants** (empresas), administrar sus **accesos** y dejar **auditoría** de todo. Tiene **BD propia** (PostgreSQL) y **no toca la BD de Natural**: todo cambio sobre empresas y usuarios lo hace llamando a la API interna `api/Plataforma/*` de Natural con una clave de servicio.

> Plan de trabajo: `../workplan_plataforma.md`. Estado: fases F1–F5 (esqueleto, seguridad, persistencia, empresas, accesos, soporte y auditoría).

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
| `GET /Empresas/{id}/Accesos` | Personas de la empresa y su acceso (usuario, rol, estado) |
| `POST /Empresas/{id}/Accesos` | Crea el acceso de una persona (usuario, contraseña SHA-256 base64, rol 01/02/03) |
| `PATCH /Empresas/{id}/Accesos/{usuarioId}/Estado?activo=` | Activa/desactiva un acceso |
| `PATCH /Empresas/{id}/Accesos/{usuarioId}/Rol` | Cambia el rol |
| `PUT /Empresas/{id}/Accesos/{usuarioId}/Contrasena` | Restablece la contraseña |
| `DELETE /Empresas/{id}/Accesos/{usuarioId}` | Quita el acceso (la persona y su historial se conservan) |
| `POST /Empresas/{id}/Impersonar` | Token de soporte como admin de la empresa. Exige `motivo` (5–300) y `minutos` (5–120, defecto 30); queda auditado y registrado en `SesionSoporte` |
| `GET /Auditoria?empresaId&actorPersonaId&desde&hasta&pagina&tamano` | Consulta paginada (más reciente primero; `hasta` solo fecha = día completo) |

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

## Producción en Lightsail (Docker + nginx)

Ejemplos listos en [`deploy/`](deploy): `docker-compose.prod.yml` y `nginx.example.conf`.

1. **Imagen:** `docker pull bray09/brar-api:<version>` (o construirla en la instancia).
2. **Red interna:** `docker network create natural-net` y conectar a ella el contenedor de Natural. Brar llama a Natural por `http://<contenedor-natural>:8080/api` (variable `NaturalApi__BaseUrl`), no por la URL pública.
3. **`.env`** junto al compose, con las variables de arriba (BD de Supabase de Brar, `NaturalJwt__Key`, `NaturalApi__ClaveServicio`).
4. **En Natural:** variable `Plataforma__ClaveServicio` con el **mismo** valor que `NaturalApi__ClaveServicio`.
5. **nginx:** subdominio `brar.naturalback.vip` apuntando a `127.0.0.1:8081` (certificado con certbot) y, en el server de Natural, `location ~* ^/v2/api/plataforma { return 404; }` para cerrar la API interna al público.
6. **Primer arranque:** `RunMigrationsOnStartup=true`; luego quitarlo.
7. **Verificar:** `curl https://brar.naturalback.vip/health` → `{"estado":"ok",...}`; `curl https://naturalback.vip/v2/api/Plataforma/Empresas` → `404`.

`UseForwardedHeaders=true` (ya puesto en el compose de producción) hace que la auditoría guarde la IP real del cliente; solo es seguro porque el puerto de Brar se publica en `127.0.0.1`.
