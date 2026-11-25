# CLAUDE.md — Laboratorio 3

Contexto para Claude Code. Leer esto y `PLAN.md` antes de hacer cualquier cosa.

## Qué es

Trabajo práctico de la materia **Laboratorio 3** (UBP).
Sitio de reviews de películas, series y animes, estilo IMDb.
Autor: **Santiago Juri Nam**. Usar el nombre completo en README, footer, "Acerca de" y cualquier documento de entrega.

**`PLAN.md` es la fuente de verdad.** Si algo de este archivo choca con el plan, preguntar antes de decidir.

## Cómo trabajar conmigo

- Responder en español. Frases cortas y directas.
- Avanzar **una fase por vez** (fases en `PLAN.md` §9).
- Al terminar una fase:
  1. Verificar que compila y corre.
  2. Actualizar la sección **Estado** de este archivo.
  3. Proponer el mensaje de commit.
  4. Esperar mi OK antes de pasar a la siguiente fase.
- Si una decisión no está en el plan, preguntar. No inventar alcance.
- Ejemplos de código inline en la respuesta cuando explique algo.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core MVC, .NET 10 LTS |
| Vistas | Razor + Bootstrap 5 + JS mínimo con `fetch` |
| DB | PostgreSQL 17 (`postgres:17-alpine`) |
| ORM | EF Core + Npgsql, migraciones versionadas |
| Auth | ASP.NET Core Identity |
| APIs | TMDB, única fuente del catálogo (películas, series y anime) + YouTube (embed + Data API v3) |
| Deploy | Docker Compose en un LXC de Proxmox, expuesto con Cloudflare Tunnel |

## Entorno

**Desarrollo (esta PC, Windows):**
- Repo: `C:\Users\Santi\Documents\UBP\Laboratorio3`
- Remoto: `https://github.com/Jurl1/Laboratorio3` (rama `main`)
- Postgres local con Docker Desktop, usando el mismo compose.

**Producción (servidor casero):**
- Proxmox VE. Ya existen LXC `100` (media) y `101` (juegos). Este proyecto usa un LXC nuevo, `104` / `lab3` (el 102 está ocupado).
- URL pública: `https://lab3.juriserver.website`
- Tunnel de Cloudflare **propio** llamado `lab3`. No reutilizar el tunnel ni el Nginx Proxy Manager de los otros LXC.
- El LXC se borra entero cuando apruebe la materia. Nada de este proyecto debe quedar fuera del LXC salvo el tunnel y el DNS en Cloudflare.

**Claude Code corre en la PC, no en el servidor.** Para pasos en Proxmox o en el LXC: darme los comandos exactos para que yo los corra por SSH. No asumir acceso al servidor.

## Reglas que no se negocian

- **Secretos:** nunca en git. Van en `deploy/.env` (ya ignorado por `*.env` en `.gitignore`). Al repo solo sube `deploy/.env.example` con valores vacíos. Revisar `git status` antes de proponer un commit.
- **API keys:** solo del lado del servidor. Nunca en JS, vistas ni respuestas al navegador.
- **Contraseñas:** solo vía Identity. Nada de hashing propio.
- **Postgres:** sin `ports:` publicados en producción.
- **TMDB:** atribución obligatoria (logo + "This product uses the TMDB API but is not endorsed or certified by TMDB"). "Dónde ver" atribuye a JustWatch.
- **Una sola API de catálogo:** TMDB. No agregar Jikan, OMDb ni otras fuentes sin preguntar. El anime sale de TMDB (género `16` + keyword `210024`). A IMDb solo se linkea con `imdb_id`.
- **YouTube Data API:** ~100 búsquedas por día. Cachear 7 días por título. Trailers siempre por embed (`youtube-nocookie.com`), que no gasta cuota.
- **Formularios POST:** anti-forgery token siempre.
- **HTML de usuario:** no se acepta. Las reviews son texto plano escapado por Razor.

## Estructura objetivo

```
Laboratorio3/
├─ src/Lab3.Web/        Controllers, Views, Models, Services, Data
├─ tests/Lab3.Tests/    opcional
├─ deploy/
│  ├─ Dockerfile
│  ├─ docker-compose.yml
│  └─ .env.example
├─ PLAN.md
├─ CLAUDE.md
├─ .gitignore
└─ README.md
```

## Comandos

Desarrollo:

```powershell
# Postgres local
docker compose -f deploy/docker-compose.yml up -d db

# Correr la app
dotnet run --project src/Lab3.Web

# Migraciones
dotnet ef migrations add <Nombre> --project src/Lab3.Web
dotnet ef database update --project src/Lab3.Web
```

Deploy (en el LXC, lo corro yo):

```bash
cd ~/Laboratorio3
git pull
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.yml logs -f app
```

## Estado

| Fase | Descripción | Estado |
|---|---|---|
| — | Plan de implementación | ✅ Hecho (`PLAN.md`) |
| 0 | Infra: LXC, Docker, tunnel, Hello World online | 🔧 En curso: código listo (Hello World, Dockerfile, compose app + cloudflared). LXC 104 con Docker andando. Falta tunnel y verificar la URL pública |
| 1 | Esqueleto: solución, Dockerfile, compose, Postgres, primera migración | Pendiente |
| 2 | Usuarios: registro, login, logout, perfil | Pendiente |
| 3 | Catálogo: TMDB (incluye anime), cache, home, búsqueda, detalle con trailer y link a IMDb | Pendiente |
| 4 | Reviews: CRUD, promedio, likes, spoilers | Pendiente |
| 5 | Bookmarks: estados, "Mis listas", perfil público | Pendiente |
| 6 | YouTube extra: pestaña "Videos" | Pendiente |
| 7 | Pulido: responsive, "Acerca de", errores amigables | Pendiente |
| 8 | Hardening y entrega: checklist de seguridad, backup, README, datos demo | Pendiente |

## Pendientes a confirmar

- Si la cátedra exige un stack o una base de datos específicos.
- Fecha de entrega.
- Si el registro necesita verificación por mail (requiere SMTP).
