# Laboratorio 3 — Plan de implementación

**Proyecto:** sitio de reviews de películas, series y animes (estilo IMDb)
**Autor:** Santiago Juri Nam
**Repositorio:** https://github.com/Jurl1/Laboratorio3
**Estado:** plan — todavía sin código

---

## 1. Decisiones clave

| Tema | Decisión | Por qué |
|---|---|---|
| Backend | ASP.NET Core MVC (.NET 10 LTS) | El repo ya tiene el `.gitignore` de Visual Studio. Identity resuelve login y hashing de contraseñas. |
| Vistas | Razor + Bootstrap 5 + un poco de JS (`fetch`) | Server-side rendering. Sin SPA, menos piezas. |
| Base de datos | PostgreSQL 17 | Liviana en un contenedor. EF Core + Npgsql. |
| ORM | Entity Framework Core + migraciones | El esquema queda versionado en el repo. |
| Usuarios | ASP.NET Core Identity | Hash PBKDF2, lockout, tokens de reset ya resueltos. |
| Catálogo (películas, series y anime) | TMDB API, única fuente | Gratis para uso no comercial con atribución. Tiene anime, trailers de YouTube, "dónde ver" y el ID de IMDb de cada título. |
| Videos | YouTube IFrame embed + YouTube Data API v3 | Embed no consume cuota. La Data API se usa solo para videos extra. |
| Deploy | Docker Compose dentro de un LXC propio | Se borra el LXC y desaparece todo. |
| Exposición | Cloudflare Tunnel propio, apuntando directo al contenedor de la app | Sin puertos abiertos. Sin Nginx Proxy Manager en el medio. |

> Si la cátedra exige otro stack (Node, PHP, Django), el plan sigue igual. Solo cambian la sección 3 y el Dockerfile.

---

## 2. Dominio

URL pública: **`https://lab3.juriserver.website`**

- `juriserver.website` ya está en Cloudflare.
- `lab3` es un subdominio nuevo. Lo crea solo el tunnel `lab3`.
- No toca los subdominios de los otros servicios.

---

## 3. Arquitectura

```
Usuario ──HTTPS──► Cloudflare (TLS, WAF)
                        │  tunnel "lab3"
                        ▼
┌──────────────── LXC 104 "lab3" (Debian 13) ────────────────┐
│  docker network: lab3_net                                   │
│                                                             │
│  cloudflared ──► app (ASP.NET Core :8080) ──► db (Postgres) │
│                        │                                    │
│                        ├──► TMDB API                        │
│                        └──► YouTube Data API                │
│                                                             │
│  volúmenes: pgdata, dataprotection-keys, backups            │
└─────────────────────────────────────────────────────────────┘
```

- Solo `cloudflared` sale a internet hacia Cloudflare. Postgres no publica puertos.
- Las API keys viven en el servidor (`.env`). El navegador nunca las ve.
- Las respuestas de las APIs externas se cachean en la DB. Menos llamadas, menos límites.

---

## 4. Qué instalar en el servidor

### 4.1 Proxmox (host)

Crear el LXC:

| Parámetro | Valor |
|---|---|
| ID / nombre | `104` / `lab3` |
| Template | Debian 13 (trixie) standard |
| Tipo | Unprivileged |
| Features | `nesting=1`, `keyctl=1` (necesarios para Docker) |
| CPU / RAM / Swap | 2 vCPU / 2 GB / 512 MB |
| Disco | 16 GB |
| Red | DHCP o IP fija en tu LAN |

### 4.2 Dentro del LXC

| Paquete | Para qué |
|---|---|
| `docker-ce`, `docker-ce-cli`, `containerd.io` | Motor de contenedores (repo oficial de Docker) |
| `docker-compose-plugin` | `docker compose` |
| `git` | Clonar y actualizar el repo |
| `curl`, `ca-certificates`, `gnupg` | Necesarios para agregar el repo de Docker |
| `unattended-upgrades` | Parches de seguridad automáticos |
| `ufw` (opcional) | Bloquear todo lo entrante salvo SSH desde la LAN |

No hace falta instalar .NET ni Postgres en el LXC. Todo corre en contenedores.

### 4.3 Contenedores (`docker-compose.yml`)

| Servicio | Imagen | Notas |
|---|---|---|
| `app` | Build propio (multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`) | Escucha en `:8080`. Lee la config desde `.env`. |
| `db` | `postgres:17-alpine` | Volumen `pgdata`. Sin `ports:` publicados. |
| `cloudflared` | `cloudflare/cloudflared:latest` | `tunnel run --token ${CF_TUNNEL_TOKEN}` |
| `backup` (opcional) | `prodrigestivill/postgres-backup-local` | `pg_dump` diario a un volumen. Útil para la demo. |
| `adminer` (opcional, solo LAN) | `adminer` | Publicado solo en la IP de la LAN. Nunca por el tunnel. |

Agregar el label de Watchtower solo si querés updates automáticos. Para la entrega conviene tener versiones fijas.

### 4.4 Cloudflare

1. Zero Trust → Networks → Tunnels → **Create tunnel** → nombre `lab3`.
2. Copiar el token al `.env` del LXC (`CF_TUNNEL_TOKEN`).
3. Public Hostname: `lab3.juriserver.website` → Service `http://app:8080`.
4. Cloudflare crea solo el CNAME.
5. SSL/TLS: modo **Full**. Activar "Always Use HTTPS".
6. Opcional: una regla de rate limiting sobre `/Account/Login`.

### 4.5 Cuentas y API keys (gratis)

| Servicio | Qué sacar | Dónde |
|---|---|---|
| TMDB | API Read Access Token (v4 bearer) | themoviedb.org → Settings → API |
| Google Cloud | Proyecto + YouTube Data API v3 habilitada + API key restringida a esa API | console.cloud.google.com |
| SMTP (opcional) | Para confirmar mail y resetear contraseña: Brevo free o Gmail con app password | — |

### 4.6 En tu PC (desarrollo)

- .NET 10 SDK
- Visual Studio 2026 o VS Code + C# Dev Kit
- Herramienta EF: `dotnet tool install --global dotnet-ef`
- Docker Desktop (para levantar Postgres local con el mismo compose)
- Git (el repo ya está clonado)

---

## 5. Modelo de datos

```
AspNetUsers (Identity)
  Id, UserName, Email, PasswordHash, EmailConfirmed, LockoutEnd, ...
  + DisplayName, AvatarUrl, Bio, CreatedAt

Titles                       ← cache local de lo que viene de TMDB
  Id (PK)
  TmdbId        int
  TmdbType      enum: Movie | Tv      (endpoint de TMDB)
  IsAnime       bool                  (se calcula al sincronizar)
  ImdbId        string, nullable      (ej: "tt0213338", para linkear a IMDb)
  Name, OriginalName, Year
  Overview, PosterPath, BackdropPath
  GenresJson
  TrailerYoutubeKey
  LastSyncedAt
  UNIQUE (TmdbType, TmdbId)

Reviews
  Id (PK), UserId (FK), TitleId (FK)
  Rating        1–10
  Body          texto
  HasSpoilers   bool
  CreatedAt, UpdatedAt
  UNIQUE (UserId, TitleId)    ← una review por usuario por título

ReviewLikes
  UserId (FK), ReviewId (FK)  PK compuesta

Bookmarks
  UserId (FK), TitleId (FK)   PK compuesta
  Status        enum: Pendiente | Viendo | Visto | Abandonado
  CreatedAt

ApiCache
  Key (PK)      ej: "tmdb:trending:week"
  Payload       jsonb
  ExpiresAt
```

Reglas:
- El puntaje promedio de la comunidad se calcula con `AVG(Rating)` sobre `Reviews`.
- Un `Title` se crea la primera vez que alguien lo abre, reseña o guarda.
- Borrar un usuario borra sus reviews, likes y bookmarks (cascade).

---

## 6. Integraciones externas

### TMDB (películas, series y anime)

| Uso | Endpoint |
|---|---|
| Home: tendencias | `GET /trending/{movie\|tv}/week` |
| Búsqueda | `GET /search/multi?query=` |
| Detalle | `GET /movie/{id}` y `GET /tv/{id}` con `append_to_response=videos,credits,watch/providers,similar,external_ids,keywords` |
| Trailer | Del campo `videos`, filtrar `site=YouTube` y `type=Trailer` → `key` |
| Dónde ver en Argentina | `watch/providers` → `results.AR` |
| Link a IMDb | `external_ids.imdb_id` → `https://www.imdb.com/title/{imdb_id}` |
| Imágenes | `https://image.tmdb.org/t/p/w500/{path}` |

Atribución obligatoria: logo de TMDB y el texto "This product uses the TMDB API but is not endorsed or certified by TMDB" en el footer o en "Acerca de". "Dónde ver" también pide atribuir a JustWatch.

### Anime (también desde TMDB)

TMDB no tiene un género "Anime". Se arma con dos filtros: género **Animación** (`16`) + keyword **anime** (`210024`).

| Uso | Endpoint |
|---|---|
| Home: anime popular | `GET /discover/tv?with_genres=16&with_keywords=210024&sort_by=popularity.desc` |
| Películas de anime | `GET /discover/movie?with_genres=16&with_keywords=210024` |
| Pestaña "Anime" en búsqueda | Filtrar los resultados de `/search/multi` que tengan género `16` y origen `JP` |

Regla para `IsAnime` al guardar un título: tiene género `16` **y** (keyword `210024` **o** `origin_country` contiene `JP`).

Cache: listas del home cada 6 h. Detalle de un título cada 24 h.

### YouTube

| Uso | Cómo | Cuota |
|---|---|---|
| Mostrar trailer | `<iframe src="https://www.youtube-nocookie.com/embed/{key}">` | No consume |
| "Reviews en YouTube" de un título | `search.list?q={nombre} review&type=video&maxResults=6` | ~100 búsquedas por día por proyecto |
| Datos del video (vistas, duración) | `videos.list?id=...` | 1 unidad |

Como la búsqueda es el recurso escaso: cachear 7 días por título y solo buscar cuando el usuario abre la pestaña "Videos".

### Patrón en el código

```
Services/
  ITmdbClient      → TmdbClient      (HttpClient tipado, bearer token)
  IYouTubeClient   → YouTubeClient   (API key)
  ICatalogService  → normaliza a TitleDto, marca IsAnime, maneja cache
```

Los controllers solo hablan con `ICatalogService`.

---

## 7. Páginas

| Ruta | Contenido | Login |
|---|---|---|
| `/` | Tendencias de películas y series, anime popular, últimas reviews de la comunidad | No |
| `/buscar?q=` | Resultados unificados con pestañas Películas / Series / Anime | No |
| `/titulo/{tipo}/{id}` | Póster, datos, trailer, reparto, dónde ver, puntaje comunidad vs. TMDB, link a IMDb, reviews, videos de YouTube | No |
| `/titulo/.../review` | Crear o editar tu review | Sí |
| `/usuario/{username}` | Perfil público: reviews y bookmarks | No |
| `/mis-listas` | Bookmarks por estado | Sí |
| `/cuenta/*` | Registro, login, logout, olvidé mi contraseña, editar perfil | — |
| `/admin` (opcional) | Moderar reviews | Rol Admin |

Bookmark y like se hacen con `fetch` a endpoints `POST /api/bookmarks` y `POST /api/reviews/{id}/like`, sin recargar la página.

---

## 8. Seguridad

- Contraseñas: hash de Identity (PBKDF2). Nunca texto plano.
- Política: mínimo 8 caracteres. Lockout después de 5 intentos fallidos.
- Rate limiting de ASP.NET Core en login y registro.
- Anti-forgery token en todos los formularios POST.
- Cookies `Secure`, `HttpOnly`, `SameSite=Lax`.
- `ForwardedHeaders` configurado para leer la IP real del cliente detrás de Cloudflare.
- Data Protection keys persistidas en un volumen. Si no, cada rebuild desloguea a todos.
- Reviews: se guardan como texto y Razor las escapa. Sin HTML del usuario.
- Secretos en `.env`. El `.gitignore` ya ignora `*.env`. Subir un `.env.example` sin valores.
- Headers: `Content-Security-Policy` permitiendo solo `image.tmdb.org` y `youtube-nocookie.com`.

---

## 9. Fases

| # | Fase | Resultado verificable |
|---|---|---|
| 0 | **Infra primero** | LXC creado, Docker andando, tunnel activo. Un "Hello World" de ASP.NET visible en `lab3.juriserver.website`. |
| 1 | Esqueleto | Solución .NET, Dockerfile, compose, Postgres conectado, primera migración. |
| 2 | Usuarios | Registro, login, logout, perfil. Usuario guardado en la DB con hash. |
| 3 | Catálogo | Cliente TMDB, filtro de anime, cache, home, búsqueda, página de detalle con trailer y link a IMDb. |
| 4 | Reviews | CRUD de reviews, puntaje promedio, likes, spoilers ocultos. |
| 5 | Bookmarks | Guardar con estado, página "Mis listas", perfil público. |
| 6 | YouTube extra | Pestaña "Videos" con `search.list` cacheado. |
| 7 | Pulido | Diseño responsive, página "Acerca de" con atribuciones, mensajes de error amigables. |
| 8 | Hardening y entrega | Checklist de la sección 8, backup probado, README con instrucciones de deploy, datos de demo cargados. |

Hacer la fase 0 primero es a propósito. Los problemas de red y tunnel aparecen temprano y no el día de la entrega.

---

## 10. Flujo de trabajo

1. Programar en la PC con Postgres en Docker Desktop.
2. `git push` a `main`.
3. En el LXC: `git pull && docker compose up -d --build`.
4. Las migraciones se aplican solas al arrancar la app (`Database.Migrate()`).

Opcional más adelante: GitHub Actions que construya la imagen y la suba a GHCR. El LXC solo hace `docker compose pull`.

### Estructura del repo

```
Laboratorio3/
├─ src/
│  └─ Lab3.Web/            (MVC: Controllers, Views, Models, Services, Data)
├─ tests/
│  └─ Lab3.Tests/          (opcional: tests de servicios)
├─ deploy/
│  ├─ docker-compose.yml
│  ├─ .env.example
│  └─ Dockerfile
├─ PLAN.md                (este archivo)
├─ CLAUDE.md              (contexto y reglas para Claude Code)
├─ .gitignore
└─ README.md
```

---

## 11. Variables de entorno (`.env`)

```
POSTGRES_DB=lab3
POSTGRES_USER=lab3
POSTGRES_PASSWORD=<generar>
ConnectionStrings__Default=Host=db;Database=lab3;Username=lab3;Password=<igual>
Tmdb__ReadAccessToken=<token>
YouTube__ApiKey=<key>
Smtp__Host= / Smtp__User= / Smtp__Password=   (opcional)
CF_TUNNEL_TOKEN=<token>
ASPNETCORE_ENVIRONMENT=Production
```

---

## 12. Baja completa (después de aprobar)

1. Exportar un último backup si lo querés guardar (`pg_dump`).
2. Proxmox: `pct stop 104 && pct destroy 104 --purge`.
3. Cloudflare Zero Trust: borrar el tunnel `lab3`.
4. Cloudflare DNS: borrar el CNAME `lab3` (no siempre se borra solo).
5. Google Cloud: borrar la API key o el proyecto entero.
6. TMDB: revocar el token si no lo vas a usar más.

Borrar el LXC no toca el tunnel ni el DNS. Por eso los pasos 3 y 4 son aparte.

---

## 13. Pendientes a confirmar

- Si la cátedra pide un stack o una base de datos específicos.
- Fecha de entrega, para repartir las fases.
- Si hace falta verificar el mail en el registro (requiere SMTP).
