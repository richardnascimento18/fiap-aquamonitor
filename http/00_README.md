# AquaMonitor — HTTP Files para Kulala.nvim

## Setup

### 1. Iniciar a API

**Via dotnet (requer .NET 8 SDK):**
```bash
cd AquaMonitor.API
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet run
# API em http://localhost:5178
```

**Via Docker (recomendado — zero dependências externas):**
```bash
# Na raiz do projeto (onde está o Dockerfile)
docker build -t aquamonitor-api .
docker run --env-file .env -p 8080:8080 aquamonitor-api
# API em http://localhost:8080
```
> Para Docker, edite `http-client.private.env.json` e troque o ambiente para `"docker"` no Kulala.

---

### 2. Configurar ambiente no Kulala

No Neovim, após abrir qualquer arquivo `.http`:

```
:lua require("kulala").set_selected_env("dev")
```

Ou via keybind padrão do Kulala (`<leader>Se` para selecionar env).

---

### 3. Fluxo de teste recomendado

Execute os arquivos **nesta ordem**:

```
01_auth.http      → Login admin → copie o token
02_sensors.http   → Crie um sensor → anote o ID
03_readings.http  → Registre leituras (inclusive com anomalia)
04_alerts.http    → Gerencie os alertas gerados
05_reports.http   → Gere relatórios de impacto
```

### 4. Após Login admin — atualizar o token

Após executar **"Login admin"** em `01_auth.http`, copie o valor do campo `"token"` da resposta e cole no `http-client.private.env.json`:

```json
{
  "dev": {
    "base_url": "http://localhost:5178",
    "admin_token": "SEU_TOKEN_AQUI",
    "token": "",
    "sensor_id": "1",
    "alert_id": "1",
    "report_id": "1"
  }
}
```

Depois de criar um sensor, atualize `sensor_id` com o `id` retornado.  
Depois de registrar uma leitura com anomalia, atualize `alert_id` com o `alertId` retornado.  
Depois de gerar um relatório, atualize `report_id` com o `id` retornado.

---

### 5. Credenciais privadas

Não há administrador padrão. Siga a configuração de bootstrap local no README principal e defina `admin_email` e `admin_password` em `http-client.private.env.json` (ignorado pelo Git). Salve tokens somente nesse arquivo privado. Para Docker, prepare `.env` a partir de `.env.example` com uma chave JWT nova.

---

### 6. Referência de status de alerta

| Valor | Status        |
|-------|---------------|
| `0`   | Open          |
| `1`   | Acknowledged  |
| `2`   | Resolved      |

### 7. Referência de severidade de alerta

| Valor | Severidade | Condição              |
|-------|------------|-----------------------|
| `1`   | Low        | vazão < 1.5× limiar   |
| `2`   | Medium     | 1.5× ≤ vazão < 2×     |
| `3`   | High       | 2× ≤ vazão < 3×       |
| `4`   | Critical   | vazão ≥ 3× limiar     |
