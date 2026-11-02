# language: pt
@BDD
Funcionalidade: Transparência dos indicadores de consumo
  Como administrador
  Quero comparar períodos de consumo
  Para planejar ações de conservação sem atribuir causalidade à redução observada

  Cenário: Comparar junho com maio usando consumos conhecidos
    Dado que foram consumidos 2000 litros em maio e 1500 litros em junho
    Quando gero o relatório de junho de 2025
    Então o relatório deve apresentar 1,5 metro cúbico de consumo
    E a diferença observada deve ser 0,5 metro cúbico e 25 por cento
