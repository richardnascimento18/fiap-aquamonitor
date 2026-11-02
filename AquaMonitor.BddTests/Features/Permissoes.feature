# language: pt
@BDD
Funcionalidade: Operações administrativas
  Cenário: Administrador remover sensor sem histórico
    Dado que sou administrador e existe um sensor sem histórico
    Quando solicito a remoção desse sensor
    Então o sensor deve ser removido

  Cenário: Usuário comum não pode remover sensores
    Dado que possuo um sensor ativo com limite de 100 litros por hora
    Quando solicito a remoção desse sensor
    Então a remoção deve ser negada e o sensor preservado
