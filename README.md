# catalogo

A API de um serviço de streaming, em C# e .NET 8: catálogo, temporadas,
episódios, elenco, perfis, controle parental, minha lista, continuar
assistindo, avaliações, busca e a montagem da tela inicial.

É a espinha dorsal do **CondePlay** — o resto do sistema (player HLS, site,
leitor de MP4, recomendações e telemetria) consome daqui.

```
GET /titulos?tamanho=2

{
  "itens": [
    {
      "id": 1,
      "tipo": "Filme",
      "nome": "Cidade de Vidro",
      "ano": 2024,
      "classificacao": "12",
      "duracaoEmMinutos": 118,
      "generos": ["ficcao"]
    },
    …
  ],
  "proximoCursor": "aXwyfDIwMjYtMDktMjNUMTM6MjI6MTUuNTE5NDAwMCswMDowMA"
}
```

## O catálogo de demonstração

Os títulos, as sinopses e os nomes são **inventados para este projeto**. As
mídias apontam para fluxos HLS **públicos de teste** — os da Mux e o exemplo
oficial da Apple —, publicados justamente para quem está desenvolvendo player.

Nenhum arquivo de vídeo entra no repositório, e não há arte de capa: a tela
desenha a capa a partir do nome e de uma cor derivada do id. Além de manter o
repositório leve, isso evita trazer conteúdo de terceiros para dentro dele sem
necessidade nenhuma.

## Por que existe

Um CRUD de catálogo parece trivial até você tentar acertar cinco coisas.

### 1. Classificação indicativa não ordena por texto

As faixas brasileiras são L, 10, 12, 14, 16 e 18. Alfabeticamente, **"10" vem
antes de "L"** — e "18" também. Um controle parental que guarda o selo como
texto e ordena com `ORDER BY` libera filme adulto para criança.

Aqui o valor guardado é a **idade mínima** (0, 10, 12, …), que ordena e compara
sozinha. O texto é só apresentação. E um selo desconhecido é **recusado**, em
vez de cair em `Livre` — porque cair em `Livre` por desconhecimento é
exatamente o erro que se quer evitar.

### 2. O filtro etário mora na consulta, não na tela

Se ele ficar só na montagem da tela inicial, o catálogo vaza pela busca, pela
página de gênero, pelo link direto e pela lista salva antes de o perfil virar
infantil.

E um título bloqueado responde **404, não 403**: dizer "existe mas você não
pode ver" já entrega que ele existe.

### 3. "Continuar assistindo" tem três regras, e todas irritam quando erradas

- **Abrir e fechar em dez segundos não é assistir.** Sem um piso, a fileira
  enche do que a pessoa só espiou. Mas em episódio de 3 minutos, 2% são 4
  segundos — por isso vale o maior entre a fração e um piso em segundos.
- **Quem chega a 92% viu os créditos.** Sem um teto, o filme terminado fica
  preso na fileira para sempre.
- **Série aponta para o próximo episódio.** Terminar o episódio 3 não tira a
  série da fileira: ela passa a apontar para o 4, no segundo zero — e isso
  precisa atravessar a virada de temporada, senão a série some justamente
  quando a pessoa mais provavelmente ia continuar.

### 4. Paginação por cursor, não por OFFSET

Num catálogo que muda enquanto a pessoa rola a tela, o offset **repete e pula**
itens: entrou um título no topo, tudo desceu uma posição, e o item 24 vira o 25
e aparece de novo na página seguinte.

O cursor guarda o par `(chave da ordenação, id)` do último item e pede "o que
vem depois deste par". O `id` entra porque a chave sozinha empata — dois
títulos do mesmo ano precisam de um desempate estável, senão a fronteira da
página oscila.

Ele vai em base64url e opaco de propósito: um cursor que parece um valor
convida quem consome a API a montá-lo na mão, e aí mudar a ordenação vira
quebra de contrato.

### 5. Busca sem acento precisa de coluna própria

O SQLite não tira acento: `LIKE '%cao%'` não acha "coração", e o `LIKE` dele só
ignora maiúsculas em ASCII. A saída é guardar o nome já normalizado numa coluna
e comparar normalizado com normalizado — o que também permite indexar, o que
normalizar na consulta impediria.

E cada termo vale sozinho: quem digita "aco grande" acha "O Grande Aço".

## A armadilha do SQLite que derrubou 12 testes

O provedor recusa, com `NotSupportedException`, qualquer `ORDER BY` sobre
`DateTimeOffset`:

```
SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY clauses.
```

Faz sentido: o SQLite não tem tipo de data, guarda texto — e comparar
`"2026-09-24T12:00:00-03:00"` com `"2026-09-24T11:00:00-06:00"` daria a ordem
errada, porque o segundo é **depois** e alfabeticamente vem antes.

A correção é um conversor na convenção:

```csharp
construtor.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
```

Grava um número que ordena por instante absoluto e ainda preserva o fuso.
Sem ele, a listagem do catálogo simplesmente não roda.

## Os endpoints

```
GET    /titulos                      lista com busca, filtros e cursor
GET    /titulos/{id}                 o título inteiro, com temporadas e elenco
POST   /titulos                      cria
PUT    /titulos/{id}                 altera
DELETE /titulos/{id}                 tira do catálogo (remoção lógica)
POST   /titulos/{id}/restaurar       traz de volta

POST   /titulos/{id}/temporadas                              cria temporada
PUT    /titulos/{id}/temporadas/{t}                          altera
DELETE /titulos/{id}/temporadas/{t}                          apaga
POST   /titulos/{id}/temporadas/{t}/episodios                cria episódio
PUT    /titulos/{id}/temporadas/{t}/episodios/{e}            altera
DELETE /titulos/{id}/temporadas/{t}/episodios/{e}            apaga

GET    /generos                      com a contagem de cada um

GET    /perfis                       POST, PUT /{id}, DELETE /{id}
GET    /perfis/{id}/lista            POST e DELETE /{tituloId}
GET    /perfis/{id}/progresso        PUT para gravar onde parou
GET    /perfis/{id}/avaliacoes       PUT para avaliar, DELETE para desfazer

GET    /inicio/{perfilId}            destaque e fileiras, já filtrados
GET    /saude                        e /swagger em desenvolvimento
```

Filtros da listagem: `busca`, `genero`, `tipo`, `anoDe`, `anoAte`, `ordem`
(`Recentes`, `Alfabetica`, `Lancamento`), `cursor`, `tamanho`, `perfilId`.

### Decisões de comportamento

- **Marcar na lista duas vezes não é erro**, é clique duplo: responde 200 com o
  estado final, e a tela não precisa saber se já estava lá.
- **O progresso é limitado à duração.** O player relata a posição a cada poucos
  segundos, e um relatório atrasado chega com valor maior que a mídia inteira.
- **Remover título é lógico; remover perfil é físico.** O título pode voltar no
  mês que vem com o histórico intacto; o histórico de alguém que pediu para
  apagar tem que sumir de verdade.
- **A duração é copiada para o progresso.** Se a mídia for reenviada com outra
  duração, o percentual de quem assistiu antes passaria a ser calculado contra
  outro número — e um filme concluído voltaria para "continuar assistindo".

## Estrutura

```
Core/Modelo/            título, temporada, episódio, pessoa, perfil, progresso
Core/Modelo/ClassificacaoIndicativa.cs   a faixa etária que ordena por idade
Core/Perfis/ContinuarAssistindo.cs       as três regras da fileira
Core/Perfis/ControleParental.cs          o filtro que mora na consulta
Core/Consulta/                           filtros, cursor e normalização da busca
Core/Inicio/MontadorDeInicio.cs          as fileiras, sem repetição e sem vazias
Core/Armazenamento/                      EF Core, índices e o catálogo de exemplo
Api/Endpoints/                           catálogo, administração, perfis, início
```

## Rodando

```bash
dotnet run --project src/Catalogo.Api
# http://localhost:5000/swagger
```

O banco é um arquivo SQLite criado na primeira execução, já semeado com o
catálogo de demonstração.

```bash
dotnet test
```

54 testes: 35 das regras puras (classificação, continuar assistindo, controle
parental, cursor, normalização, montagem da tela) e 19 de integração por HTTP
de verdade, contra um SQLite em memória.

O teste que mais vale: a paginação percorre o catálogo inteiro de duas em duas
e exige que **nenhum id se repita e nenhum falte** — que é exatamente o que o
`OFFSET` não garante.

## Limites conhecidos

- **Sem autenticação.** Não há conta, login nem token: o `perfilId` vai na
  requisição e pronto. Num serviço de verdade isso é a primeira coisa a existir.
- **Sem paginação nas fileiras da tela inicial.** Cada uma traz 20 itens e
  acabou; a rolagem lateral infinita exigiria um cursor por fileira.
- **A busca é `LIKE`**, não índice de texto completo: sem relevância, sem
  tolerância a erro de digitação, sem radicalização. Para isso existe o
  `indice-invertido`, que é outro projeto deste mesmo perfil.
- **Sem cache.** Toda tela inicial refaz as consultas.
- **SQLite.** Serve para desenvolver e para os testes; um catálogo de verdade
  com muitos perfis escrevendo progresso ao mesmo tempo pediria Postgres.
- **Sem upload de mídia.** A API guarda a URL da playlist; quem produz o HLS é
  outra parte do sistema.

## Licença

MIT.
