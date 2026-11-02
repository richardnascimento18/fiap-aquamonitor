# language: pt
@BDD
Funcionalidade: Acesso à conta
  Como pessoa responsável pelo consumo de água
  Quero acessar minha conta com segurança
  Para acompanhar meus sensores

  Cenário: Cadastrar uma conta e acessar os sensores
    Dado que ainda não possuo uma conta
    Quando cadastro meus dados válidos
    Então minha conta deve ser criada como usuário comum
    Quando entro com minha senha correta
    Então devo conseguir consultar meus sensores

  Cenário: Recusar uma senha incorreta
    Dado que possuo uma conta cadastrada
    Quando tento entrar com uma senha incorreta
    Então não devo receber uma sessão válida
