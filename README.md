# AquaMonitor API — ESG Tema 7: Acesso à Água e Preservação de Recursos Naturais

## Visão Geral

O **AquaMonitor** é um sistema de monitoramento inteligente do consumo de água desenvolvido em **.NET Core 8**, seguindo a arquitetura **MVVM**, com endpoints RESTful, autenticação JWT, paginação, validação avançada e detecção automática de vazamentos.

O sistema permite que empresas e instituições registrem sensores de medição hídrica, coletem leituras de consumo em tempo real, recebam alertas automáticos quando a vazão ultrapassa limites configurados e gerem relatórios de impacto ambiental com planos de mitigação.

---

## Pré-requisitos

| Ferramenta | Versão mínima |
|---|---|
| .NET SDK | 8.0 |
| libsqlite3 (Linux) | 3.x |
| Docker (opcional) | 20.x |

> **Importante:** o projeto utiliza SQLite via chamadas nativas ao `libsqlite3.so.0`, dispensando qualquer pacote NuGet externo de banco de dados. O único pacote externo necessário para os **testes** é o xUnit, que será restaurado automaticamente pelo `dotnet restore` em um ambiente com acesso à internet.

---

## Como Executar o Projeto

### Opção 1 — Execução direta com .NET CLI

```bash
# 1. Entre na pasta da API
cd AquaMonitor.API

# 2. Execute a aplicação
dotnet run
```

A API estará disponível em `http://localhost:5178`.

O banco de dados SQLite (`aquamonitor.db`) será criado automaticamente na primeira execução, incluindo o schema completo e o usuário administrador padrão.

### Opção 2 — Docker

```bash
# Na raiz do projeto (onde está o Dockerfile)
docker build -t aquamonitor-api .
docker run -p 8080:8080 -v aquamonitor-data:/app/data aquamonitor-api
```

A API estará disponível em `http://localhost:8080`.

---

## Credenciais Padrão

O sistema cria automaticamente um usuário administrador na inicialização:

| Campo | Valor |
|---|---|
| E-mail | `admin@aquamonitor.com` |
| Senha | `Admin@123` |
| Role | `Admin` |

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
│   │   ├── SqliteConnection.cs       # Wrapper P/Invoke sobre libsqlite3
│   │   ├── SqliteConnectionFactory.cs
│   │   ├── DatabaseInitializer.cs    # Schema DDL + seed (equivalente às migrations)
│   │   └── PasswordHelper.cs         # PBKDF2-SHA256 (sem BCrypt NuGet)
│   ├── Filters/
│   │   └── GlobalExceptionFilter.cs  # Tratamento centralizado de exceções
│   ├── JwtAuthHandler.cs             # Handler JWT customizado (sem JwtBearer NuGet)
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
4. A requisição **"Login admin"** popula a variável `adminToken`, utilizada nas rotas protegidas
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

### Testes implementados

| Classe | Testes | Descrição |
|---|---|---|
| `AuthControllerTests` | 4 | Register 201, Login 200, credenciais inválidas 401, e-mail duplicado 409 |
| `WaterSensorsControllerTests` | 4 | Listagem 200, criação 201, ID inválido 404, sem token 401 |
| `WaterReadingsControllerTests` | 4 | Listagem 200, criação 201, anomalia gera alerta, sumário 200 |
| `LeakAlertsControllerTests` | 2 | Listagem 200, ID inválido 404 |
| `WaterImpactReportsControllerTests` | 2 | Listagem 200, ID inválido 404 |

---

## Decisões Técnicas

### Banco de Dados
O projeto utiliza **SQLite** via chamadas P/Invoke diretas ao `libsqlite3.so.0` (biblioteca nativa do sistema operacional), implementadas na classe `SqliteConnection`. Essa abordagem elimina dependências de NuGet externas mantendo todas as funcionalidades esperadas — consultas, transações, índices e paginação.

O schema do banco é criado automaticamente na inicialização via `DatabaseInitializer`, que executa instruções `CREATE TABLE IF NOT EXISTS` — funcionando como um sistema de migrações idempotente.

### Autenticação JWT
O JWT HS256 é gerado e validado manualmente via `System.Security.Cryptography.HMACSHA256`, sem o pacote `Microsoft.AspNetCore.Authentication.JwtBearer`. O handler customizado `JwtAuthHandler` integra-se nativamente ao pipeline de autenticação do ASP.NET Core 8.

### Segurança de Senhas
As senhas são armazenadas com **PBKDF2-SHA256** (100.000 iterações, salt aleatório de 16 bytes), implementado via `System.Security.Cryptography.Rfc2898DeriveBytes` — sem o pacote BCrypt.Net.

### Paginação
Todos os endpoints de listagem implementam paginação via `LIMIT/OFFSET` no SQLite, retornando o objeto `PagedResultVM<T>` com `totalCount`, `totalPages`, `hasNextPage` e `hasPreviousPage`.

### Detecção Automática de Anomalias
Ao registrar uma leitura via `POST /api/WaterReadings`, o sistema compara a vazão informada com o limiar configurado no sensor. Se excedido, um `LeakAlert` é criado automaticamente com severidade proporcional (Low / Medium / High / Critical).

---

## Variáveis de Ambiente

| Variável | Padrão | Descrição |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `aquamonitor.db` | Caminho do arquivo SQLite |
| `Jwt__Key` | (definido em appsettings) | Chave secreta JWT — **alterar em produção** |
| `Jwt__ExpiresInHours` | `8` | Validade do token em horas |
