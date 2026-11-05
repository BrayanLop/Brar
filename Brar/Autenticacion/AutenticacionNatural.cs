using System.Security.Claims;
using System.Text;
using Brar.Configuracion;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Brar.Autenticacion;

/// <summary>Nombres de claim propios de Brar (el token de Natural se traduce a estos al validarse).</summary>
public static class BrarClaims
{
    public const string PersonaId = "personaId";
    public const string Nombre = "nombre";
    public const string Rol = "rol";
}

public static class Politicas
{
    /// <summary>Solo el superadmin de plataforma (rol 00 de Natural).</summary>
    public const string Plataforma = "Plataforma";
    public const string RolPlataforma = "00";
}

/// <summary>
/// Brar no emite tokens: acepta el mismo JWT que emite Natural (firma, issuer, audience y expiración) y
/// exige el rol 00. Natural arma el token con JwtSecurityToken, así que nombre y rol pueden llegar con el
/// tipo corto o el largo de .NET; se aceptan ambos.
/// </summary>
public static class AutenticacionNatural
{
    private static readonly string[] TiposPersonaId = ["sub", ClaimTypes.NameIdentifier];
    private static readonly string[] TiposNombre = ["name", "unique_name", ClaimTypes.Name];
    private static readonly string[] TiposRol = ["role", "roles", ClaimTypes.Role];

    public static IServiceCollection AddAutenticacionNatural(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Sin mapeo: se trabaja con los nombres de claim tal como vienen en el token.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = ConfiguracionRequerida.NaturalIssuer(config),
                    ValidAudience = ConfiguracionRequerida.NaturalAudience(config),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["NaturalJwt:Key"]!)),
                    ClockSkew = TimeSpan.FromMinutes(2),
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = Traducir };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.Plataforma, p => p
                .RequireAuthenticatedUser()
                .RequireClaim(BrarClaims.Rol, Politicas.RolPlataforma));

        return services;
    }

    /// <summary>Reemplaza la identidad por una con los claims de Brar. Sin <c>sub</c> entero el token se rechaza.</summary>
    private static Task Traducir(TokenValidatedContext context)
    {
        var origen = context.Principal!;

        if (!int.TryParse(Primero(origen, TiposPersonaId), out var personaId) || personaId <= 0)
        {
            context.Fail("El token de Natural no trae un sub válido.");
            return Task.CompletedTask;
        }

        var claims = new List<Claim>
        {
            new(BrarClaims.PersonaId, personaId.ToString()),
            new(BrarClaims.Nombre, Primero(origen, TiposNombre) ?? "desconocido"),
        };
        // Un token puede traer varios roles: se copian todos.
        claims.AddRange(origen.Claims.Where(c => TiposRol.Contains(c.Type)).Select(c => new Claim(BrarClaims.Rol, c.Value)));

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims, JwtBearerDefaults.AuthenticationScheme, BrarClaims.Nombre, BrarClaims.Rol));
        return Task.CompletedTask;
    }

    private static string? Primero(ClaimsPrincipal principal, string[] tipos) =>
        tipos.Select(t => principal.FindFirst(t)?.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
