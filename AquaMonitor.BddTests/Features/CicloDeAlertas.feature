# language: pt
@BDD
Funcionalidade: Acompanhamento de possíveis vazamentos
  Cenário: Reconhecer, resolver e reabrir um alerta
    Dado que possuo um alerta de possível vazamento
    Quando reconheço e depois resolvo o alerta
    Então a resolução deve registrar o instante correto
    Quando reabro o alerta para nova investigação
    Então o alerta deve ficar aberto sem data de resolução
