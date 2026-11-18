using OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Options;
using OficinaMecanica.AuthLambda.Infrastructure.Identidade.Options;

namespace OficinaMecanica.AuthLambda.Function.Configuration;

internal sealed class AuthLambdaConfiguration
{
    private const int DefaultExpirationMinutes = 60;

    private AuthLambdaConfiguration(string connectionString, JwtOptions jwtOptions, ClientesApiOptions clientesApiOptions)
    {
        ConnectionString = connectionString;
        JwtOptions = jwtOptions;
        ClientesApiOptions = clientesApiOptions;
    }

    public string ConnectionString
    {
        get;
    }

    public JwtOptions JwtOptions
    {
        get;
    }

    public ClientesApiOptions ClientesApiOptions { get; }

    public static AuthLambdaConfiguration Carregar()
    {
        var valorHabilitado = Environment.GetEnvironmentVariable("ClientesApi__Habilitado") ?? "false";
        if (!bool.TryParse(valorHabilitado, out var habilitado))
        {
            throw new InvalidOperationException("Configuração da consulta privada de clientes inválida.");
        }
        var clientesApiOptions = new ClientesApiOptions
        {
            Habilitado = habilitado,
            EnderecoBase = Environment.GetEnvironmentVariable("ClientesApi__EnderecoBase") ?? string.Empty,
            Token = Environment.GetEnvironmentVariable("ClientesApi__Token") ?? string.Empty
        };
        clientesApiOptions.Validar();
        var connectionString = habilitado ? string.Empty : Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer");
        if (!habilitado && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configuração de banco de dados ausente.");
        }

        var jwtOptions = new JwtOptions
        {
            Issuer = Environment.GetEnvironmentVariable("Jwt__Issuer") ?? string.Empty,
            Audience = Environment.GetEnvironmentVariable("Jwt__Audience") ?? string.Empty,
            Secret = Environment.GetEnvironmentVariable("Jwt__Secret") ?? string.Empty,
            ExpirationMinutes = ObterExpirationMinutes()
        };

        jwtOptions.Validar();

        return new AuthLambdaConfiguration(connectionString ?? string.Empty, jwtOptions, clientesApiOptions);
    }

    private static int ObterExpirationMinutes()
    {
        var expirationValue = Environment.GetEnvironmentVariable("Jwt__ExpirationMinutes");
        if (!int.TryParse(expirationValue, out var expirationMinutes))
        {
            return DefaultExpirationMinutes;
        }

        if (expirationMinutes <= 0)
        {
            return DefaultExpirationMinutes;
        }

        return expirationMinutes;
    }
}
