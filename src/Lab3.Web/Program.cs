var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Fase 0: Hello World para probar LXC + Docker + Cloudflare Tunnel.
app.MapGet("/", () => Results.Content("""
    <!doctype html>
    <html lang="es">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>Laboratorio 3</title>
    </head>
    <body style="font-family: system-ui, sans-serif; max-width: 40rem; margin: 4rem auto; padding: 0 1rem;">
      <h1>Hello World — Laboratorio 3</h1>
      <p>Fase 0: infraestructura andando.</p>
      <footer><small>Santiago Juri Nam · UBP</small></footer>
    </body>
    </html>
    """, "text/html; charset=utf-8"));

app.MapGet("/healthz", () => Results.Ok("ok"));

app.Run();
