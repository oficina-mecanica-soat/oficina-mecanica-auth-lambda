using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.AuthLambda.Application.Common;
using OficinaMecanica.AuthLambda.Application.Identidade.Repositories;
using OficinaMecanica.AuthLambda.Domain.Atendimento.Enums;
using OficinaMecanica.AuthLambda.Domain.Atendimento.ValueObjects;
using OficinaMecanica.AuthLambda.Infrastructure;
using OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Options;
using OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Repositories;
using OficinaMecanica.AuthLambda.Infrastructure.Identidade.Options;

namespace OficinaMecanica.AuthLambda.Function.UnitTests.Atendimento.Repositories;

public sealed class ApiClienteAutenticacaoRepositoryTests
{
    private const string TokenTeste = "credencial-tecnica-exclusiva-teste-clientes-2026";

    private static ClientesApiOptions CriarOptions() => new()
    {
        Habilitado = true,
        EnderecoBase = "http://api.interna.test/",
        Token = TokenTeste
    };

    private static HttpResponseMessage Resposta(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Dado_ConsultaPrivada_Quando_Obter_Entao_DeveEnviarDocumentoNoCorpoETokenSegregado(bool ativo)
    {
        // Arrange
        var clienteId = Guid.NewGuid();
        string? corpo = null;
        var handler = new RespostaHandler(async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsoluteUri.Should().Be("http://api.interna.test/internal/v1/clientes/consultar-autenticacao");
            request.Headers.GetValues("X-Clientes-Internos-Token").Single().Should().Be(TokenTeste);
            request.RequestUri.AbsoluteUri.Should().NotContain("52998224725");
            corpo = await request.Content!.ReadAsStringAsync(ct);
            return Resposta($$"""{"clienteId":"{{clienteId}}","ativo":{{ativo.ToString().ToLowerInvariant()}}}""");
        });
        using var client = new HttpClient(handler);
        var repository = new ApiClienteAutenticacaoRepository(client, CriarOptions());

        // Act
        var cliente = await repository.ObterAsync(CpfCnpj.Criar("529.982.247-25"), default);

        // Assert
        cliente.Should().Be(new ClienteAutenticacao(clienteId, ativo ? StatusCliente.Ativo : StatusCliente.Inativo));
        corpo.Should().Be("{\"documento\":\"52998224725\"}");
    }

    [Fact]
    public async Task Dado_AusenciaConfirmada_Quando_Consultar_Entao_DeveRetornarNull()
    {
        // Arrange
        using var client = new HttpClient(new RespostaHandler((_, _) => Task.FromResult(Resposta("{\"clienteId\":null,\"ativo\":false}"))));
        var repository = new ApiClienteAutenticacaoRepository(client, CriarOptions());

        // Act
        var resultado = await repository.ObterAsync(CpfCnpj.Criar("52998224725"), default);

        // Assert
        resultado.Should().BeNull();
    }

    [Theory]
    [InlineData(301)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Dado_FalhaHttp_Quando_Consultar_Entao_NaoDeveConfundirComClienteInexistente(int status)
    {
        // Arrange
        using var client = new HttpClient(new RespostaHandler((_, _) => Task.FromResult(Resposta("nao-expor-documento-ou-token", (HttpStatusCode)status))));
        var repository = new ApiClienteAutenticacaoRepository(client, CriarOptions());

        // Act
        var tentativa = () => repository.ObterAsync(CpfCnpj.Criar("52998224725"), default);

        // Assert
        var erro = await tentativa.Should().ThrowAsync<DependenciaIndisponivelException>();
        erro.Which.Message.Should().NotContain("nao-expor").And.NotContain(TokenTeste).And.NotContain("52998224725");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{")]
    [InlineData("{\"clienteId\":null}")]
    [InlineData("{\"ativo\":false}")]
    [InlineData("{\"clienteId\":null,\"ativo\":true}")]
    [InlineData("{\"clienteId\":\"00000000-0000-0000-0000-000000000000\",\"ativo\":false}")]
    [InlineData("{\"clienteId\":\"invalido\",\"ativo\":false}")]
    [InlineData("{\"clienteId\":null,\"ativo\":false,\"nome\":\"nao-aceitar\"}")]
    public async Task Dado_RespostaInvalida_Quando_Consultar_Entao_DeveClassificarDependenciaIndisponivel(string json)
    {
        // Arrange
        using var client = new HttpClient(new RespostaHandler((_, _) => Task.FromResult(Resposta(json))));
        var repository = new ApiClienteAutenticacaoRepository(client, CriarOptions());

        // Act
        var tentativa = () => repository.ObterAsync(CpfCnpj.Criar("52998224725"), default);

        // Assert
        await tentativa.Should().ThrowAsync<DependenciaIndisponivelException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_TimeoutOuFalhaTransporte_Quando_Consultar_Entao_DevePropagarIndisponibilidade(bool timeout)
    {
        // Arrange
        Exception erro = timeout ? new TaskCanceledException("timeout-teste") : new HttpRequestException("rede-teste");
        using var client = new HttpClient(new RespostaHandler((_, _) => Task.FromException<HttpResponseMessage>(erro)));
        var repository = new ApiClienteAutenticacaoRepository(client, CriarOptions());

        // Act
        var tentativa = () => repository.ObterAsync(CpfCnpj.Criar("52998224725"), default);

        // Assert
        await tentativa.Should().ThrowAsync<DependenciaIndisponivelException>();
    }

    [Fact]
    public async Task Dado_CancelamentoDoChamador_Quando_Consultar_Entao_DevePreservarCancelamento()
    {
        // Arrange
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        using var client = new HttpClient(new RespostaHandler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct)));
        var repository = new ApiClienteAutenticacaoRepository(client, CriarOptions());

        // Act
        var tentativa = () => repository.ObterAsync(CpfCnpj.Criar("52998224725"), cancelamento.Token);

        // Assert
        await tentativa.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://api.interna.test")]
    [InlineData("https://usuario:senha@api.interna.test")]
    [InlineData("https://api.interna.test/api/")]
    [InlineData("https://api.interna.test/?documento=valor")]
    public void Dado_EnderecoInvalido_Quando_Configurar_Entao_DeveRejeitarSemExporValores(string endereco)
    {
        // Arrange
        var options = new ClientesApiOptions { Habilitado = true, EnderecoBase = endereco, Token = TokenTeste };

        // Act
        var tentativa = () => options.Validar();

        // Assert
        tentativa.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain(TokenTeste).And.NotContain("usuario:senha");
    }

    [Theory]
    [InlineData("")]
    [InlineData("curto")]
    [InlineData("token-com-espaco-nao-deve-ser-aceito 2026")]
    public void Dado_TokenInvalido_Quando_Configurar_Entao_DeveRejeitar(string token)
    {
        // Arrange
        var options = new ClientesApiOptions { Habilitado = true, EnderecoBase = "https://api.interna.test", Token = token };

        // Act
        var tentativa = () => options.Validar();

        // Assert
        tentativa.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dado_ModoApi_Quando_Compor_Entao_NaoDeveRegistrarRepositorySqlOuFallback()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuthLambdaInfrastructure(string.Empty, new JwtOptions(), CriarOptions());
        using var provider = services.BuildServiceProvider();

        // Act
        var repositories = provider.GetServices<IClienteAutenticacaoRepository>().ToArray();

        // Assert
        repositories.Should().ContainSingle().Which.Should().BeOfType<ApiClienteAutenticacaoRepository>();
    }

    private sealed class RespostaHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public RespostaHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responder(request, cancellationToken);
        }
    }
}
