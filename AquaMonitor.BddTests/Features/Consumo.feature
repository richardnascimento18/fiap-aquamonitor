# language: pt
@BDD
Funcionalidade: Monitoramento de consumo e vazamentos
  Como responsável pela conservação de água
  Quero identificar consumo anormal
  Para investigar possíveis desperdícios

  Cenário: Registrar consumo abaixo do limite sem gerar alerta
    Dado que possuo um sensor ativo com limite de 100 litros por hora
    Quando registro uma leitura de 50 litros por hora
    Então a leitura deve ser persistida sem anomalia
    E nenhum alerta deve ser criado

  Esquema do Cenário: Classificar a severidade nos limites de consumo anormal
    Dado que possuo um sensor ativo com limite de 100 litros por hora
    Quando registro uma leitura de <vazao> litros por hora
    Então a leitura deve ser persistida como anômala
    E um alerta de severidade "<severidade>" deve ser criado

    Exemplos:
      | vazao | severidade |
      | 150   | Medium     |
      | 200   | High       |
      | 300   | Critical   |

  Cenário: Rejeitar medição negativa sem alterar o histórico
    Dado que possuo um sensor ativo com limite de 100 litros por hora
    Quando registro uma leitura de -1 litros por hora
    Então a medição inválida deve ser recusada
    E nenhum alerta deve ser criado
