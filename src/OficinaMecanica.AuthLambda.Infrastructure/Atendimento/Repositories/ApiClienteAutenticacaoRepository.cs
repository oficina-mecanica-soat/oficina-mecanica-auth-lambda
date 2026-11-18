using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OficinaMecanica.AuthLambda.Application.Common;
using OficinaMecanica.AuthLambda.Application.Identidade.Repositories;
using OficinaMecanica.AuthLambda.Domain.Atendimento.Enums;
using OficinaMecanica.AuthLambda.Domain.Atendimento.ValueObjects;
using OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Contratos;
using OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Options;

namespace OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Repositories;

public sealed class ApiClienteAutenticacaoRepository : IClienteAutenticacaoRepository
{
    public const string CaminhoConsulta = "internal/v1/clientes/consultar-autenticacao";
    public const string HeaderToken = "X-Clientes-Internos-Token";
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly HttpClient _httpClient;
    private readonly ClientesApiOptions _options;

    public ApiClienteAutenticacaoRepository(HttpClient httpClient, ClientesApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validar();
        if (!options.Habilitado)
        {
            throw new InvalidOperationException("A consulta privada de clientes não está habilitada.");
        }
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<ClienteAutenticacao?> ObterAsync(CpfCnpj documento, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.EnderecoBase), CaminhoConsulta))
            {
                Content = JsonContent.Create(new ConsultarClienteApiRequest(documento.Numero), options: OpcoesJson)
            };
            request.Headers.Add(HeaderToken, _options.Token);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                // Inclusive 404: uma rota indisponível não equivale a um cliente inexistente.
                throw new HttpRequestException("A consulta privada não foi confirmada.", null, response.StatusCode);
            }
            var cliente = await response.Content.ReadFromJsonAsync<ClienteAutenticacaoApiResponse>(OpcoesJson, cancellationToken)
                ?? throw new JsonException("Resposta privada ausente.");
            if (cliente.ClienteId == Guid.Empty || cliente.Ativo && !cliente.ClienteId.HasValue)
            {
                throw new JsonException("Resposta privada incompatível.");
            }
            return cliente.ClienteId.HasValue
                ? new ClienteAutenticacao(cliente.ClienteId.Value, cliente.Ativo ? StatusCliente.Ativo : StatusCliente.Inativo)
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException or IOException or TimeoutException)
        {
            throw new DependenciaIndisponivelException("A consulta privada de clientes está indisponível.", exception);
        }
    }
}
