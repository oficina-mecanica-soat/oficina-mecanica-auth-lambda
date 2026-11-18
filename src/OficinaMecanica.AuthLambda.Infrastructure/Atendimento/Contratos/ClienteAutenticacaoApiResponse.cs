using System.Text.Json.Serialization;

namespace OficinaMecanica.AuthLambda.Infrastructure.Atendimento.Contratos;

internal sealed record ClienteAutenticacaoApiResponse(
    [property: JsonRequired] Guid? ClienteId,
    [property: JsonRequired] bool Ativo);
