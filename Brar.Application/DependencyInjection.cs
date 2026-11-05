using Brar.Application.Auditoria;
using Brar.Application.Empresas;
using Microsoft.Extensions.DependencyInjection;

namespace Brar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditoriaServicio>();
        services.AddScoped<EmpresasServicio>();
        return services;
    }
}
