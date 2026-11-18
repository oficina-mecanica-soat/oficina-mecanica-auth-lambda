# 🔐 Oficina Mecânica Auth Lambda

Autentica clientes por documento válido e ativo e emite JWT com perfil Cliente. No modo distribuído, consulta OS por HTTP privado com token técnico distinto do JWT, sem conexão direta ao SQL Server. API Gateway publica `POST /auth/documento`.

<a id="arquitetura"></a>

## 🏗️ Arquitetura

Domain valida o documento; Application coordena a autenticação; Infrastructure consulta o cadastro e assina o JWT; Function adapta o contrato do Gateway. O token contém a identidade do cliente, sem CPF/CNPJ. Os três microsserviços validam a mesma chave, issuer e audience; permissões continuam dependentes do perfil.


## 📡 Contrato HTTP esperado

O contrato exposto pelo API Gateway é:

```http
POST /auth/documento
```

### Request

```json
{
  "documento": "529.982.247-25"
}
```

### Response 200

Exemplo com `Jwt__ExpirationMinutes=30`:

```json
{
  "accessToken": "...",
  "tokenType": "Bearer",
  "expiresIn": 1800
}
```

Com a configuração default atual de 60 minutos, `expiresIn` será `3600`.

### Response 400

```json
{
  "mensagem": "CPF/CNPJ inválido.",
  "tipo": "Validacao"
}
```

### Response 401

```json
{
  "mensagem": "Documento não autorizado.",
  "tipo": "NaoAutorizado"
}
```

### Response 503

```json
{
  "mensagem": "Serviço temporariamente indisponível.",
  "tipo": "DependenciaIndisponivel"
}
```

---

<a id="variaveis-de-ambiente"></a>

## ⚙️ Variáveis de ambiente

| Variável | Obrigatória | Uso |
| --- | --- | --- |
| `ConnectionStrings__SqlServer` | Somente no modo legado | Connection string do SQL Server usado para consulta de cliente. |
| `ClientesApi__Habilitado` | Não | `false` por padrão; `true` seleciona exclusivamente a consulta HTTP privada de OS. |
| `ClientesApi__EnderecoBase` | No modo privado | Raiz do NLB interno, sem caminho, query ou fragmento. |
| `ClientesApi__Token` | No modo privado | Credencial técnica exclusiva, ASCII imprimível de 32 a 256 caracteres, distinta do JWT. |
| `Jwt__Issuer` | Sim | Issuer do JWT. Valor esperado: `oficina-mecanica-auth`. |
| `Jwt__Audience` | Sim | Audience do JWT. Valor esperado: `oficina-mecanica-api`. |
| `Jwt__Secret` | Sim | Chave HMAC-SHA256 usada para assinar o token. |
| `Jwt__ExpirationMinutes` | Não | Expiração em minutos. Usa `60` como default se ausente ou inválido. |
| `AWS_LAMBDA_EXEC_WRAPPER` | Sim no deploy AWS | Ativa o wrapper da layer Datadog. |
| `DD_API_KEY_SECRET_ARN` | Sim no deploy AWS | ARN do secret criado para a API key Datadog. |
| `DD_SITE` | Sim no deploy AWS | Site Datadog; a configuração atual usa `datadoghq.com`. |
| `DD_SERVICE`, `DD_ENV`, `DD_VERSION` | Sim no deploy AWS | Identificação e correlação da telemetria. |

Observações:

- `Jwt__Secret` deve ter pelo menos 32 bytes.
- Secrets reais não devem ser versionados.
- Credenciais de banco, tokens e chaves devem ser configurados em ambiente seguro na etapa de deploy.

### GitHub Environment `development`

Crie os itens abaixo em **Settings > Environments > development**:

| Nome | Tipo | Valor esperado em termos conceituais |
| --- | --- | --- |
| `AWS_ACCESS_KEY_ID` | Environment Secret | Access key temporária do AWS Academy. |
| `AWS_SECRET_ACCESS_KEY` | Environment Secret | Secret key temporária do AWS Academy. |
| `AWS_SESSION_TOKEN` | Environment Secret | Token temporário da sessão AWS Academy. |
| `AUTH_LAMBDA_JWT_SECRET` | Environment Secret | Chave HMAC forte, compatível com a validação da API. |
| `DD_API_KEY` | Environment Secret | API key do Datadog; o workflow grava o valor no AWS Secrets Manager. |
| `AWS_REGION` | Environment Variable | Região AWS, com fallback `us-east-1`. |
| `AUTH_LAMBDA_EXECUTION_ROLE_NAME` | Environment Variable | Nome da role externa do laboratório, normalmente `LabRole`. |
| `DD_SITE` | Environment Variable | Site Datadog aplicável; a configuração atual efetiva usa `datadoghq.com`. |
| `AUTO_PR_ENABLED` | Repository Variable | `true` somente quando as promoções automáticas estiverem habilitadas. |
| `RELEASE_BRANCH` | Repository Variable | Branch de promoção, com fallback `release`. |

O ambiente VocLabs fornece a `LabRole`; este repositório apenas consulta e associa essa role à função. Ele não cria nem gerencia policies IAM da role externa.

Além da tabela, configurar `TERRAFORM_STATE_KEY`, `AUTH_LAMBDA_JWT_ISSUER`, `AUTH_LAMBDA_JWT_AUDIENCE`, `CLIENTES_INTERNOS_TOKEN` e `CLIENTES_API_ENABLED=true` para a consulta privada. `AUTH_LAMBDA_JWT_SECRET` precisa corresponder a `JWT_SECRET` dos três serviços. Preservar a chave de proteção do state; não rotacionar como tentativa de corrigir restauração.

### Deploy, destroy e validação

O merge em `develop` chama o workflow AWS quando há mudança deployável. `infra/terraform/environments/dev/terraform-action.env` controla `apply` ou `destroy`; destroy deve ocorrer em PR dedicado. Após `apply`, a esteira valida configuração, VPC, layers, secret Datadog e contratos SSM. Após `destroy`, confirma a remoção da função e dos contratos próprios.

A Lambda publica `/oficina-mecanica/development/auth-lambda/function_arn`, `function_name` e `/oficina-mecanica/development/status/auth-lambda`. No fluxo distribuído, `CLIENTES_API_ENABLED=true` consulta clientes pela API privada e remove a conexão SQL da função; a credencial técnica é distinta do JWT. O modo SQL é legado.

Seu apply vem depois da API com consulta privada disponível e antes do Gateway. O JWT precisa ser aceito pelos três serviços. Seguir a [ordem de referência das oito esteiras](https://github.com/oficina-mecanica-soat/oficina-mecanica-api#deploy-aws); não aplicar Terraform localmente.

---

<a id="seguranca"></a>

## 🔒 Segurança

Decisões já implementadas:

- Query SQL fixa e parametrizada.
- Sem uso de `AddWithValue`.
- JWT não contém CPF, CNPJ ou documento do cliente.
- Cliente inexistente e cliente inativo retornam resposta indistinguível.
- Erros inesperados são sanitizados e não retornam `exception.Message`.
- Validação de CPF/CNPJ fica no domínio.
- Secret JWT inválido falha em modo fail-fast.
- Checagem local de pacotes vulneráveis faz parte da validação do repositório.

O deploy usa as layers versionadas `dd-trace-dotnet` e `Datadog-Extension`. A instrumentação está configurada, mas a ingestão de logs e traces da Lambda no Datadog não foi evidenciada de ponta a ponta no ambiente acadêmico e permanece como evolução pós-entrega.

---

<a id="testes-e-qualidade"></a>

## 🧪 Testes e qualidade

O repositório possui testes unitários por camada, seguindo o padrão adotado na solução:

- nomes BDD no formato `Dado_Quando_Entao`;
- padrão AAA explícito com `Arrange`, `Act` e `Assert`;
- FluentAssertions para asserções;
- Moq quando há dependência a simular;
- factories de teste para dados comuns;
- cobertura relevante em Application, Domain e Function.

### Comandos principais

```powershell
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --collect:"XPlat Code Coverage" --no-restore
dotnet list package --vulnerable --include-transitive
```

### Validação antes de PR

```powershell
dotnet build -c Release --no-restore
dotnet test -c Release --collect:"XPlat Code Coverage" --no-restore
dotnet list package --vulnerable --include-transitive
git diff --check
```

---

<a id="git-flow-e-governanca"></a>

<a id="operação-e-documentação"></a>

## 🧭 Operação e documentação

- [Visão geral, arquitetura e ordem das esteiras](https://github.com/oficina-mecanica-soat/oficina-mecanica-api#deploy-aws).
- [Configuração, primeira aplicação e recuperação após Reset](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/blob/develop/docs/deploy/deploy-aws.md).
- [Collection consolidada e ambientes Local/Development](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/blob/develop/docs/evidencias/postman.md).

Todo provisionamento, atualização e destroy na AWS ocorre pelas esteiras oficiais. O controle é `infra/terraform/environments/dev/terraform-action.env`; destroy exige alteração dedicada e verificação dos consumidores. `AWS_DEPLOY_ENABLED` autoriza a janela real, e `AUTO_PR_ENABLED` controla somente a abertura de promoções. Alterar uma Variable não dispara o CD.

Um ambiente disponível pode ser testado sem destroy. Sem estado compatível, alguns owners exigem primeiro uma recuperação por destroy dedicado com inventário vazio comprovado. Não apague recursos ou caches para contornar o bloqueio; consulte o guia antes de iniciar uma conta ou retomar um laboratório.

Fluxo Git: branch de trabalho → PR para `develop` → `release` → `main`. O CD físico ocorre em development; release e production registram promoções lógicas. PRs e merges permanecem sujeitos aos checks e à revisão. Mudanças somente em README/docs não exigem deploy físico pelos detectores atuais.
