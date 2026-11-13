using Brar.Application;
using Brar.Autenticacion;
using Brar.Configuracion;
using Brar.Infrastructure;
using Brar.Infrastructure.Persistencia;
using Brar.Middleware;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Falla al arrancar (con el nombre de la variable) si falta configuración obligatoria.
ConfiguracionRequerida.Validar(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAutenticacionNatural(builder.Configuration);

// CORS: orígenes en Cors:AllowedOrigins (Cors__AllowedOrigins__0, __1...). Sin ninguno se permite cualquier
// origen (el token va en el header Authorization, no en cookies).
const string PoliticaCors = "Brar";
var origenes = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
    .Where(o => !string.IsNullOrWhiteSpace(o)).ToArray() ?? [];
builder.Services.AddCors(options => options.AddPolicy(PoliticaCors, policy =>
{
    if (origenes.Length > 0) policy.WithOrigins(origenes);
    else policy.AllowAnyOrigin();
    policy.AllowAnyHeader().AllowAnyMethod();
}));

// Detrás de nginx: sin esto la IP que se audita sería la del proxy. Solo se activa con UseForwardedHeaders=true,
// y únicamente si Brar NO es alcanzable directamente desde internet (solo por nginx / red interna de Docker),
// porque confiar en X-Forwarded-For permite falsificar la IP si cualquiera puede llamar a Brar.
if (builder.Configuration.GetValue<bool>("UseForwardedHeaders"))
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                             | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

builder.Services.AddExceptionHandler<ErrorApiHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Con una BD vacía (docker compose) se aplican las migraciones al arrancar.
if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<BrarDbContext>().Database.Migrate();
}

// Si Brar vive bajo un prefijo de URL del proxy (p. ej. /brar) y el proxy NO lo recorta, se define PathBase=/brar.
// Si el proxy sí lo recorta (lo habitual) o Brar tiene su propio subdominio, se deja vacío.
if (app.Configuration.GetValue<bool>("UseForwardedHeaders"))
    app.UseForwardedHeaders();

var pathBase = app.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
    app.UsePathBase(pathBase.StartsWith('/') ? pathBase : "/" + pathBase);

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors(PoliticaCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Para las pruebas de integración (WebApplicationFactory).
public partial class Program;
