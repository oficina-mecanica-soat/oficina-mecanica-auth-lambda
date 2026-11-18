using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.AuthLambda.Application.Identidade.Interfaces;
using OficinaMecanica.AuthLambda.Application.Identidade.Repositories;
using OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Options;
using OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Repositories;
using OficinaMecanica.AuthLambda.Infrastructure.Identidade.Options;
using OficinaMecanica.AuthLambda.Infrastructure.Identidade.Services;

namespace OficinaMecanica.AuthLambda.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthLambdaInfrastructure(
        this IServiceCollection services,
        string connectionString,
        JwtOptions jwtOptions,
        ClientesApiOptions? clientesApiOptions = null)
    {
        services.AddSingleton(jwtOptions);

        var options = clientesApiOptions ?? new ClientesApiOptions();
        options.Validar();
        services.AddSingleton(options);
        if (options.Habilitado)
        {
            services.AddHttpClient<IClienteAutenticacaoRepository, ApiClienteAutenticacaoRepository>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.MaxResponseContentBufferSize = 16 * 1024;
            }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            }).RedactLoggedHeaders(_ => true);
        }
        else
        {
            services.AddSingleton<IClienteAutenticacaoRepository>(new SqlClienteAutenticacaoRepository(connectionString));
        }

        services.AddSingleton<ITokenService, TokenService>();

        return services;
    }
}
