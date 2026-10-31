# AquaMonitor API — ESG Tema 7: Acesso à Água e Preservação de Recursos Naturais

## Visão Geral

O **AquaMonitor** é um sistema de monitoramento inteligente do consumo de água desenvolvido em **.NET Core 8**, seguindo a arquitetura **MVVM**, com endpoints RESTful, autenticação JWT, paginação, validação avançada e detecção automática de vazamentos.

O sistema permite que empresas e instituições registrem sensores de medição hídrica, coletem leituras de consumo em tempo real, recebam alertas automáticos quando a vazão ultrapassa limites configurados e gerem relatórios de impacto ambiental com planos de mitigação.

---

## Pré-requisitos

| Ferramenta | Versão mínima |
|---|---|
| .NET SDK | 8.0.427 (conforme `global.json`) |
| Docker (opcional) | 20.x |

> As dependências de autenticação, SQLite e testes são restauradas pelo NuGet. O driver `Microsoft.Data.Sqlite` inclui a biblioteca nativa SQLite para as plataformas suportadas.

---

## Como Executar o Projeto

### Opção 1 — Execução direta com .NET CLI

```bash
# 1. Entre na pasta da API
cd AquaMonitor.API

# 2. Configure uma chave privada (uma vez por ambiente de desenvolvimento)
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"

# 3. Execute a aplicação
dotnet run
```

A API estará disponível em `http://localhost:5178`.

O banco de dados SQLite (`aquamonitor.db`) será criado automaticamente na primeira execução, incluindo o schema completo. Nenhum usuário é criado automaticamente.

### Opção 2 — Docker

```bash
# Na raiz do projeto (onde está o Dockerfile)
docker build -t aquamonitor-api .
# Copie .env.example para .env e configure Jwt__Key com um segredo novo.
docker run --env-file .env -p 8080:8080 -v aquamonitor-data:/app/data aquamonitor-api
```

A API estará disponível em `http://localhost:8080`.

---

## Configuração de Segurança

`Jwt:Key` é obrigatório: use um segredo aleatório novo com pelo menos 32 bytes UTF-8, por variável `Jwt__Key` ou user secrets. A aplicação recusa configuração ausente ou inválida. `.env.example` contém exemplos sem credenciais; `dotnet run` não carrega arquivos `.env` automaticamente.

Para demonstrações locais, somente em `Development`, habilite explicitamente `BootstrapAdmin__Enabled=true` e forneça `BootstrapAdmin__Email` e `BootstrapAdmin__Password` (14 a 1024 caracteres) por configuração privada. A criação é idempotente e não promove contas existentes. O bootstrap de inicialização continua proibido em Production.

Para uma instalação nova em Production, execute uma vez o comando offline com o mesmo volume do serviço. Exporte privadamente `ConnectionStrings__DefaultConnection`, `BootstrapAdmin__Email` e `BootstrapAdmin__Password`; o comando lê somente variáveis de ambiente e exige um banco explicitamente configurado:

```bash
docker run --rm -v aquamonitor-data:/app/data \
  -e ConnectionStrings__DefaultConnection -e BootstrapAdmin__Email -e BootstrapAdmin__Password \
  aquamonitor-api --provision-admin
```

Use `ConnectionStrings__DefaultConnection=/app/data/aquamonitor.db` para esse volume. Também é possível executar `dotnet AquaMonitor.API.dll --provision-admin` no diretório publicado. O comando cria o schema se necessário e encerra sem iniciar HTTP; não exige chave JWT, não promove usuários existentes e não altera senhas já provisionadas. Remova as variáveis de credenciais do ambiente após o uso.

Chaves JWT, senhas de administrador e tokens publicados em versões anteriores devem ser tratados como comprometidos e substituídos nos ambientes existentes. O bootstrap não altera senhas já armazenadas.

O container usa `Production`, porta 8080 e usuário sem privilégios. Volumes vinculados devem permitir escrita pelo UID 1654 em `/app/data`. CORS bloqueia origens de navegador por padrão; configure origens exatas em `Cors__AllowedOrigins__0` (sem barra final). Clientes como curl e Postman continuam funcionando.

---

## Arquitetura do Projeto

```
AquaMonitor/
├── AquaMonitor.API/
│   ├── Controllers/        # Camada de apresentação (MVVM — View + Controller)
│   │   ├── AuthController.cs
│   │   ├── WaterSensorsController.cs
│   │   ├── WaterReadingsController.cs
│   │   ├── LeakAlertsController.cs
│   │   └── WaterImpactReportsController.cs
│   ├── ViewModels/         # DTOs de entrada e saída (MVVM — ViewModel)
│   ├── Models/             # Entidades de domínio (MVVM — Model)
│   ├── Services/           # Lógica de negócios (TokenService, AlertService)
│   ├── Data/               # Infraestrutura de dados
│   │   ├── SqliteConnection.cs       # Adaptador Microsoft.Data.Sqlite com transações
│   │   ├── SqliteConnectionFactory.cs
│   │   ├── DatabaseInitializer.cs    # Schema DDL idempotente
│   │   └── PasswordHelper.cs         # PBKDF2-SHA256, compatível com hashes existentes
│   ├── Filters/
│   │   └── GlobalExceptionFilter.cs  # Tratamento centralizado de exceções
│   └── Program.cs
├── AquaMonitor.Tests/
│   ├── AuthControllerTests.cs
│   ├── WaterSensorsControllerTests.cs
│   ├── WaterReadingsControllerTests.cs
│   ├── LeakAlertsControllerTests.cs
│   └── WaterImpactReportsControllerTests.cs
└── Dockerfile
```

---

## Endpoints Disponíveis

### Autenticação (`/api/Auth`)

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| POST | `/api/Auth/register` | Registra novo usuário | Pública |
| POST | `/api/Auth/login` | Autentica e retorna JWT | Pública |

### Sensores (`/api/WaterSensors`)

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/WaterSensors?page=1&pageSize=10` | Lista sensores paginados | JWT |
| GET | `/api/WaterSensors/{id}` | Busca sensor por ID | JWT |
| POST | `/api/WaterSensors` | Cria novo sensor | JWT |
| PUT | `/api/WaterSensors/{id}` | Atualiza sensor | JWT |
| DELETE | `/api/WaterSensors/{id}` | Remove sensor | Admin |

### Leituras (`/api/WaterReadings`)

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/WaterReadings` | Lista leituras com filtros e paginação | JWT |
| POST | `/api/WaterReadings` | Registra leitura (gera alerta automático se anomalia) | JWT |
| GET | `/api/WaterReadings/summary` | Resumo de consumo por sensor | JWT |

### Alertas (`/api/LeakAlerts`)

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/LeakAlerts` | Lista alertas com filtros e paginação | JWT |
| GET | `/api/LeakAlerts/{id}` | Busca alerta por ID | JWT |
| PATCH | `/api/LeakAlerts/{id}/status` | Atualiza status (0=Open, 1=Acknowledged, 2=Resolved) | JWT |
| GET | `/api/LeakAlerts/stats` | Estatísticas consolidadas de alertas | Admin |

### Relatórios (`/api/WaterImpactReports`)

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/WaterImpactReports` | Lista relatórios paginados | JWT |
| GET | `/api/WaterImpactReports/{id}` | Busca relatório por ID | JWT |
| POST | `/api/WaterImpactReports/generate` | Gera relatório automático para mês/ano | Admin |
| DELETE | `/api/WaterImpactReports/{id}` | Remove relatório | Admin |

---

## Como Testar com o Postman

1. Abra o Postman e importe o arquivo `AquaMonitor_Postman_Collection.json`
2. Na variável de coleção `baseUrl`, confirme o valor `http://localhost:5178` (ou `http://localhost:8080` para Docker)
3. Execute as requisições **na ordem das pastas** (01 → 05), pois os testes capturam automaticamente tokens e IDs via scripts de teste
4. Configure `adminEmail` e `adminPassword` privadamente no Postman com a conta provisionada. A requisição **"Login admin"** popula a variável `adminToken`, utilizada nas rotas protegidas
5. A requisição **"Registrar leitura com ANOMALIA"** demonstra a geração automática de alerta

### Fluxo recomendado de teste

```
01 – Login admin            → obtém adminToken
02 – Criar sensor           → obtém sensorId  
03 – Registrar leitura normal
03 – Registrar leitura com ANOMALIA → gera alertId automaticamente
04 – Listar alertas
04 – Atualizar status do alerta
05 – Gerar relatório de impacto hídrico
```

---

## Como Executar os Testes Unitários

```bash
cd AquaMonitor.Tests
dotnet restore   # necessário acesso à internet para baixar xUnit
dotnet test
```

Os testes utilizam **WebApplicationFactory** com banco de dados SQLite isolado por execução, garantindo independência entre os testes. Cada controller possui ao menos um teste que valida o **status code 200**, conforme exigido pela especificação.


---

## Decisões Técnicas

### Banco de Dados
O projeto utiliza `Microsoft.Data.Sqlite`, com consultas parametrizadas, foreign keys habilitadas em cada conexão e transações para persistir leituras e alertas atomicamente. Sensores com histórico não podem ser removidos (HTTP 409), preservando leituras e alertas.

O schema é criado por `DatabaseInitializer` com `CREATE TABLE IF NOT EXISTS`, sem recriar bancos existentes. Migrações versionadas serão necessárias para futuras mudanças de schema.

### Autenticação JWT
Os tokens HS256 são emitidos por `System.IdentityModel.Tokens.Jwt` e validados pelo middleware `Microsoft.AspNetCore.Authentication.JwtBearer`, incluindo assinatura, algoritmo, expiração, emissor, audiência, identidade e role. A tolerância de relógio é de 30 segundos.

### Segurança de Senhas
As senhas usam **PBKDF2-SHA256**, 600.000 iterações e salt aleatório de 16 bytes. Hashes existentes com 100.000 iterações continuam válidos.

### Paginação
Todos os endpoints de listagem implementam paginação via `LIMIT/OFFSET` no SQLite, retornando o objeto `PagedResultVM<T>` com `totalCount`, `totalPages`, `hasNextPage` e `hasPreviousPage`.

### Detecção Automática de Anomalias
Ao registrar uma leitura via `POST /api/WaterReadings`, o sistema compara a vazão informada com o limiar configurado no sensor. Se excedido, um `LeakAlert` é criado automaticamente com severidade proporcional (Low / Medium / High / Critical).

---

## Variáveis de Ambiente

| Variável | Padrão | Descrição |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `aquamonitor.db` | Caminho do arquivo SQLite |
| `Jwt__Key` | obrigatório, sem padrão | Segredo aleatório privado; mínimo 32 bytes UTF-8 |
| `Jwt__ExpiresInHours` | `8` | Validade do token em horas |
| `Jwt__Issuer` | `AquaMonitor.API` | Emissor JWT obrigatório |
| `Jwt__Audience` | `AquaMonitor.Client` | Audiência JWT obrigatória |
| `BootstrapAdmin__Enabled` | `false` | Bootstrap explícito, somente Development |
| `Cors__AllowedOrigins__0` | nenhuma | Origem de navegador permitida |
