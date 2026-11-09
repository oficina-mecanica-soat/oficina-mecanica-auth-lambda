# Instruções do repositório

- Preserve o contrato de autenticação e JWT definido na API em `docs/architecture/adrs` e `docs/architecture/rfcs`; avalie consumidores antes de qualquer alteração.
- Para trabalho transversal, leia `../oficina-mecanica-api/.agents/skills/oficina-mecanica/SKILL.md`; se o clone não estiver disponível, procure os documentos no GitHub da organização e informe a limitação antes de alterar contratos compartilhados. Não presuma que a skill seja descoberta automaticamente fora da API.
- Siga os padrões existentes de C#, testes e CI/CD. Use português para domínio e textos acentuados em UTF-8; mantenha termos técnicos consolidados e contratos compatíveis.
- Verifique status Git e preserve alterações. Não exponha credenciais nem execute operações AWS, deploy ou destroy sem autorização específica. Valide as mudanças e relate apenas evidências reais.
- Para mudanças .NET, use as validações do README: `dotnet restore`, `dotnet build -c Release --no-restore` e `dotnet test -c Release --collect:"XPlat Code Coverage" --no-restore`; confira o CI para requisitos adicionais. Para Terraform, verifique formatação e `validate` sem backend quando aplicável.
