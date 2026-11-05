using Brar.Application.Accesos;
using Brar.Application.Auditoria;
using Brar.Application.Empresas;
using Brar.Application.Soporte;
using Microsoft.Extensions.DependencyInjection;

namespace Brar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditoriaServicio>();
        services.AddScoped<AuditoriaConsultaServicio>();
        services.AddScoped<EmpresasServicio>();
        services.AddScoped<AccesosServicio>();
        services.AddScoped<SoporteServicio>();
        return services;
    }
}
