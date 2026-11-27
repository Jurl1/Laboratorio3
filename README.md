# Lab3: reviews de películas, series y animes

Trabajo práctico de **Laboratorio 3** (Universidad Blas Pascal).
Autor: **Santiago Juri Nam**.

Sitio estilo IMDb: buscás un título, lo puntuás del 1 al 10, escribís tu review (con aviso de spoilers), das like a las reviews de otros y armás tus listas (Pendiente, Viendo, Visto, Abandonado).

- Producción: <https://lab3.juriserver.website>
- Plan completo: [`PLAN.md`](PLAN.md)

## Funcionalidades

| Página | Ruta |
|---|---|
| Inicio: tendencias de películas y series, anime popular, últimas reviews | `/` |
| Búsqueda con pestañas Películas / Series / Anime | `/buscar?q=` |
| Detalle: póster, trailer, reparto, dónde ver (AR), puntaje comunidad vs. TMDB, link a IMDb, reviews, videos de YouTube | `/titulo/{pelicula\|serie}/{id}` |
| Escribir o editar tu review | `/titulo/{tipo}/{id}/review` |
| Perfil público con reviews y listas | `/usuario/{usuario}` |
| Mis listas por estado | `/mis-listas` |
| Registro, login, perfil, cambio y recuperación de contraseña | `/cuenta/*` |
| Acerca de y atribuciones | `/acerca` |

Likes y bookmarks se guardan con `fetch` (`POST /api/reviews/{id}/like`, `POST /api/bookmarks`), sin recargar la página.

## Stack

- ASP.NET Core MVC (.NET 10), Razor, Bootstrap 5 y JS mínimo.
- PostgreSQL 17 con EF Core + Npgsql. Las migraciones se aplican solas al arrancar.
- ASP.NET Core Identity para los usuarios (hash PBKDF2, bloqueo tras 5 intentos fallidos).
- TMDB como única fuente del catálogo, incluido el anime (género 16 + keyword 210024 u origen JP).
- YouTube: trailers embebidos con `youtube-nocookie.com`. La pestaña "Videos" usa la Data API v3 con 7 días de cache.
- Docker Compose en un LXC de Proxmox, publicado con Cloudflare Tunnel.

```
src/Lab3.Web/
  Controllers/    Home, Search, Titles, Account, Users, Api (fetch)
  Data/           AppDbContext, entidades, DemoSeeder
  Services/       Tmdb/ (cliente), Catalog/ (normaliza + cache), YouTube/, ApiCache, Email
  Infrastructure/ headers de seguridad, helpers de vistas
  Views/, wwwroot/
tests/Lab3.Tests/ tests de integración (SQLite en memoria + TMDB y YouTube falsos)
deploy/           Dockerfile, docker-compose.yml, .env.example
```

## Desarrollo local

Requisitos: .NET 10 SDK y Docker Desktop (para Postgres).

```powershell
# Postgres local (solo en 127.0.0.1, contraseña de desarrollo "lab3_dev")
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml up -d db

# Token de TMDB para el catálogo (queda fuera del repo)
dotnet user-secrets --project src/Lab3.Web set "Tmdb:ReadAccessToken" "<token>"

# Correr la app
dotnet run --project src/Lab3.Web

# Tests (no necesitan Postgres ni API keys)
dotnet test Laboratorio3.slnx

# Migraciones
dotnet tool restore
dotnet ef migrations add <Nombre> --project src/Lab3.Web
```

Sin token de TMDB, el sitio funciona igual y muestra un aviso en lugar del catálogo.

## Deploy (LXC 104)

Primera vez:

```bash
cd ~ && git clone https://github.com/Jurl1/Laboratorio3.git && cd Laboratorio3
cp deploy/.env.example deploy/.env && chmod 600 deploy/.env
nano deploy/.env    # completar CF_TUNNEL_TOKEN, POSTGRES_PASSWORD (openssl rand -hex 24), Tmdb__ReadAccessToken, YouTube__ApiKey
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

Actualizar:

```bash
cd ~/Laboratorio3 && git pull
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.yml logs -f app
```

Servicios del compose (ninguno publica puertos):

| Servicio | Qué hace |
|---|---|
| `app` | La web en `:8080`. Data Protection keys en el volumen `dataprotection-keys`. |
| `db` | `postgres:17-alpine`, datos en el volumen `pgdata`. |
| `backup` | `pg_dump` diario al volumen `backups`. Guarda 7 diarios, 4 semanales y 2 mensuales. |
| `cloudflared` | Tunnel `lab3`: `lab3.juriserver.website` → `http://app:8080`. |

### Backups

```bash
# Backup manual ahora
docker compose -f deploy/docker-compose.yml exec backup /backup.sh

# Ver los backups
docker compose -f deploy/docker-compose.yml exec backup ls -lh /backups/last

```

Restaurar el último backup. **Borra los datos actuales de la base:**

```bash
C="docker compose -f deploy/docker-compose.yml"
$C stop app
$C exec db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"'
$C exec backup sh -c 'zcat /backups/last/"$POSTGRES_DB"-latest.sql.gz | PGPASSWORD="$POSTGRES_PASSWORD" psql -h db -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
$C start app
```

### Datos de demo

En `deploy/.env`, poné `Demo__Seed=true` y una `Demo__Password` (mínimo 8 caracteres) y reiniciá la app. Se crean los usuarios `cinefila`, `seriero` y `otaku_ar`, con reviews, likes y listas sobre títulos conocidos. Si ya existen, no se vuelven a crear.

## Seguridad

- Secretos solo en `deploy/.env`, ignorado por git. Las API keys nunca llegan al navegador.
- Contraseñas con Identity (PBKDF2). Mínimo 8 caracteres y bloqueo de 15 minutos tras 5 intentos fallidos.
- Rate limiting en login, registro y recuperación de contraseña: 10 por minuto por IP. La IP real llega con `ForwardedHeaders` detrás de Cloudflare.
- Anti-forgery token en todos los POST, incluidos los `fetch` (header `RequestVerificationToken`).
- Cookies `HttpOnly`, `SameSite=Lax` y `Secure` sobre HTTPS.
- Reviews en texto plano, escapadas por Razor. No se acepta HTML.
- Headers: `Content-Security-Policy` (solo imágenes de TMDB y YouTube, iframes de `youtube-nocookie.com`, sin scripts inline), `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy` y `Permissions-Policy`.
- Postgres sin puertos publicados. Solo `cloudflared` sale a internet.

## Atribuciones

- Datos de películas, series y anime: [TMDB](https://www.themoviedb.org/). *This product uses the TMDB API but is not endorsed or certified by TMDB.*
- "Dónde ver": datos de [JustWatch](https://www.justwatch.com/) a través de TMDB.
- Trailers y videos: YouTube.
- Links a IMDb por `imdb_id`. No se usan datos de IMDb.

## Baja

Ver [`PLAN.md` §12](PLAN.md): `pct destroy 104 --purge`, borrar el tunnel `lab3` y el CNAME en Cloudflare, y revocar las keys de TMDB y Google.
