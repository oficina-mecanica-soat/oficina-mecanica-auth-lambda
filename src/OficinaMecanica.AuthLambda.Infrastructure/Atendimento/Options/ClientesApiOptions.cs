namespace OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Options;

public sealed class ClientesApiOptions
{
    public bool Habilitado { get; init; }
    public string EnderecoBase { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;

    public void Validar()
    {
        if (!Habilitado)
        {
            return;
        }
        if (!Uri.TryCreate(EnderecoBase, UriKind.Absolute, out var endereco)
            || endereco.Scheme is not ("http" or "https") || endereco.AbsolutePath != "/"
            || !string.IsNullOrEmpty(endereco.UserInfo) || !string.IsNullOrEmpty(endereco.Query)
            || !string.IsNullOrEmpty(endereco.Fragment) || Token.Length is < 32 or > 256
            || Token.Any(caractere => caractere is < '!' or > '~'))
        {
            throw new InvalidOperationException("Configuração da consulta privada de clientes inválida.");
        }
    }
}
