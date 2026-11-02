# language: pt
@BDD
Funcionalidade: Privacidade dos registros de consumo
  Cenário: Impedir alterações por outra pessoa
    Dado que um sensor e seu alerta pertencem a outra pessoa
    Quando tento alterar esse sensor e registrar consumo nele
    Então os registros da proprietária devem permanecer intactos
