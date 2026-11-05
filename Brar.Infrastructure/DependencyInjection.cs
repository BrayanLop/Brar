using Brar.Application.Auditoria;
using Brar.Application.Natural;
using Brar.Infrastructure.Natural;
using Brar.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Brar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BrarDbContext>(o =>
            o.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<IAuditoriaRepositorio, AuditoriaRepositorio>();

        var natural = configuration.GetSection("NaturalApi");
        var baseUrl = natural["BaseUrl"] ?? throw new InvalidOperationException("Falta NaturalApi__BaseUrl.");
        var clave = natural["ClaveServicio"] ?? throw new InvalidOperationException("Falta NaturalApi__ClaveServicio.");
        var timeout = int.TryParse(natural["TimeoutSegundos"], out var s) && s > 0 ? s : 15;

        services.AddHttpClient<INaturalPlataforma, NaturalPlataformaHttp>(client =>
        {
            // La base debe terminar en "/" para que las rutas relativas ("Plataforma/Empresas") se anexen bien.
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeout);
            client.DefaultRequestHeaders.Add("X-Service-Key", clave);
        });

        return services;
    }
}
