# Testes automatizados

Use o SDK .NET indicado em `global.json` (.NET 10) e Python 3. Os testes HTTP iniciam a API no processo; não é necessário subir um servidor nem configurar credenciais ou banco local. Docker é necessário apenas para o smoke test.

## Execução completa com evidências

Na raiz do repositório:

```sh
python3 scripts/verify-tests.py
```

O comando restaura dependências com `--locked-mode`, compila Release com analisadores e warnings como erros e executa quatro seleções sem sobreposição: domínio/arquitetura, regressões de integração, contratos HTTP e BDD. Cada seleção deve produzir resultados. O verificador rejeita falhas, skips, suites ausentes, redução abaixo dos mínimos versionados e evidências incompletas. Uma execução bem-sucedida termina com os totais verificados.

`TestResults/` contém TRX por seleção, `test-summary.json`, `endpoint-manifest.json`, `endpoint-coverage.json`, `schema-validation.json`, `bdd-summary.json`, `bdd.html`, `bdd.ndjson` e `evidence-gate-checks.json`. Os arquivos são gerados novamente em cada execução e não pertencem ao Git. O runner remove apenas seus próprios arquivos conhecidos. Use diretórios distintos para execuções simultâneas:

```sh
python3 scripts/verify-tests.py --results TestResults/repeticao
python3 scripts/verify-tests.py --dotnet /caminho/para/dotnet
```

O HTML do Reqnroll apresenta funcionalidades, exemplos e passos executados. O NDJSON contém Cucumber Messages; o verificador exige que cada exemplo e passo termine com sucesso, sem retries. Nove alterações controladas em cópias das evidências demonstram que o gate recusa arquivos ausentes, testes falhos ou ignorados, passos indefinidos e associações/contratos incompletos.

## Execução focada

```sh
dotnet restore AquaMonitor.sln --locked-mode
dotnet build AquaMonitor.sln --configuration Release --no-restore --warnaserror

# Todos os projetos de teste
dotnet test AquaMonitor.sln --configuration Release --no-build

# Cenários Gherkin (Reqnroll / xUnit v2)
dotnet test AquaMonitor.BddTests --configuration Release --no-build

# Contratos, schemas e descoberta de endpoints
dotnet test AquaMonitor.ContractTests --configuration Release --no-build

# Regressões existentes de serviços, HTTP, SQLite e migrações
dotnet test AquaMonitor.Tests --configuration Release --no-build --filter 'Category!=Domain&Category!=Architecture'

# Domínio e arquitetura
dotnet test AquaMonitor.Tests --configuration Release --no-build --filter 'Category=Domain|Category=Architecture'
```

Execuções focadas servem para diagnóstico; não substituem o runner de evidências. Nelas, o Reqnroll grava HTML/NDJSON em `AquaMonitor.BddTests/bin/Release/net10.0/TestResults/`. Os testes de contrato gravam evidências sob o `TestResults/` do diretório de saída; o runner direciona essas evidências para o diretório solicitado.

## Organização e isolamento

- `AquaMonitor.Tests`: regressões existentes de domínio, arquitetura, serviços, HTTP, persistência, segurança e migrações.
- `AquaMonitor.ContractTests`: testes HTTP por controller, guard de cobertura e contratos JSON independentes. As categorias são `Contract` e `ContractInfrastructure`.
- `AquaMonitor.BddTests`: seis funcionalidades em português, bindings separados por comportamento e estado por cenário. A tag `BDD` permite seleção por categoria no xUnit.
- `AquaMonitor.TestSupport`: `WebApplicationFactory`, usuários, login, provisionamento offline de administradores e relógio por cenário.

Cada novo teste HTTP e exemplo BDD possui um host e arquivo SQLite temporário exclusivos. A limpeza fecha os clientes e o host e remove banco/WAL/SHM mesmo quando um cenário falha. Não há estado mutável estático nem autenticação compartilhada. Features e classes podem executar em paralelo; não há desativação global de paralelismo.

O relógio dos novos testes inicia em 15/06/2025 UTC. O host alinha a validação de lifetime do IdentityModel a esse relógio, incluindo expiração, not-before e clock skew. Assinatura, issuer, audience, identidade e autorização continuam validados pelo middleware real. Credenciais são exclusivas de teste; administradores são criados pelo provisionamento offline existente e autenticam por HTTP. Testes de expiração avançam o relógio sem sleeps. Relatórios usam períodos e medições explícitos.

## Contratos e evolução da API

`AquaMonitor.ContractTests/endpoint-coverage.json` descreve as 18 operações, entradas, regras, autorização e expectativas positivas/negativas. O índice operacional `GET /` é a única exclusão documentada. `EndpointCoverageTests` compara métodos e rotas reais dos controllers, inclusive constraints, e verifica autenticação/roles e os Facts executáveis associados por `[Operation]`.

Cada Fact obrigatório termina com `Complete(response)`, após as asserções de comportamento e persistência. Esse helper confirma método/caminho efetivamente solicitado, status, content type e schema ou corpo vazio. A evidência não inclui payloads nem tokens. O gate cruza cada registro com o manifest de runtime, o inventário e o método que realmente passou no TRX. Descoberta de testes sozinha não representa cobertura.

Os 15 arquivos em `AquaMonitor.ContractTests/Schemas` usam Draft 2020-12 com JsonSchema.Net. Paginações reutilizam schemas de recursos por `$ref`, resolvidos localmente sem rede. Propriedades obrigatórias, tipos, limites, enumerações, datas, nullable e relações entre status/alerta são verificados. `Samples/` contém exemplos independentes de teste, não respostas geradas. Self-tests validam o meta-schema, aceitam exemplos válidos e rejeitam propriedades ausentes, tipos errados e violações de formatos, limites e referências.

Erros de validação e mensagens de aplicação possuem schemas próprios. Conflitos de integridade e falhas inesperadas usam ProblemDetails. HTTP 204 e as respostas vazias 401/403 são verificados como vazios; schema é **N/A**, não cobertura ausente. As regras de 404 para ocultar recursos de outro usuário são preservadas.

Para adicionar uma operação, atualize inventário, schemas, exemplos e Facts positivos/negativos; acrescente asserções observáveis antes de `Complete`. Atualize os mínimos em `evidence-requirements.json` somente após verificar a execução real. Novas rotas sem inventário, entradas obsoletas, Facts ausentes ou schemas inexistentes fazem a verificação falhar.

## Reprodução das verificações do GitHub Actions

```sh
dotnet restore AquaMonitor.sln --locked-mode
dotnet build AquaMonitor.sln --configuration Release --no-restore --warnaserror
dotnet format AquaMonitor.sln --verify-no-changes --no-restore --severity warn
dotnet list AquaMonitor.sln package --vulnerable --include-transitive --no-restore --format json > TestResults/dependency-audit.json
python3 scripts/ci/verify-dependency-audit.py TestResults/dependency-audit.json
python3 scripts/verify-tests.py --no-build
docker build --tag aquamonitor:verification .
python3 scripts/ci/docker-smoke.py --image aquamonitor:verification
```

Crie `TestResults/` antes do redirecionamento do audit caso ainda não tenha executado o runner. O build executa análise estática .NET; `dotnet format` verifica também os fixes de analisadores. Para lint do workflow, execute `actionlint .github/workflows/ci.yml` quando disponível.

O workflow `.github/workflows/ci.yml` preserva os checks obrigatórios `quality`, `tests` e `docker-smoke`. Na página de uma execução do GitHub Actions, baixe os artifacts `test-results` e `dependency-audit`. `test-results` inclui os quatro TRX, o HTML/NDJSON BDD e os resumos de contratos/cobertura. Retenção configurada: **60 dias**, sujeita às políticas do GitHub e à exclusão manual. Preserve a evidência acadêmica antes do vencimento; o empacotamento e documento acadêmico final são entregas separadas.
