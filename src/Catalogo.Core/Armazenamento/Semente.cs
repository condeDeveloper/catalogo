using Catalogo.Core.Modelo;

namespace Catalogo.Core.Armazenamento;

/// <summary>
/// O catálogo de demonstração.
/// </summary>
/// <remarks>
/// <para>
/// Os títulos, as sinopses e os nomes são <b>inventados para este projeto</b>.
/// Um catálogo de exemplo com obras reais traria sinopse e arte de terceiros
/// para dentro do repositório sem necessidade nenhuma — e a demonstração
/// funciona igual com conteúdo próprio.
/// </para>
/// <para>
/// As mídias apontam para fluxos HLS <b>públicos de teste</b>, publicados
/// justamente para quem está desenvolvendo player: os da Mux e o exemplo
/// oficial da Apple. Nenhum arquivo de vídeo entra no repositório — além de
/// ser desnecessário, o limite de 100 MB por arquivo do Git tornaria isso
/// inviável de qualquer jeito.
/// </para>
/// </remarks>
public static class Semente
{
    /// <summary>Um fluxo HLS público de teste.</summary>
    public const string FluxoPadrao = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8";

    /// <summary>O exemplo oficial de HLS da Apple.</summary>
    public const string FluxoApple =
        "https://devstreaming-cdn.apple.com/videos/streaming/examples/bipbop_4x3/bipbop_4x3_variant.m3u8";

    /// <summary>Outro fluxo público de teste.</summary>
    public const string FluxoAlternativo = "https://test-streams.mux.dev/pts_shift/master.m3u8";

    /// <summary>De onde vêm as mídias de demonstração.</summary>
    public const string CreditoDaMidia = "Fluxo HLS público de teste; o conteúdo do catálogo é fictício.";

    /// <summary>Preenche um banco vazio. Não faz nada se já houver título.</summary>
    public static void Semear(ContextoDoCatalogo contexto, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.Titulos.Any())
        {
            return;
        }

        var generos = new Dictionary<string, Genero>
        {
            ["drama"] = new() { Nome = "Drama", Apelido = "drama" },
            ["ficcao"] = new() { Nome = "Ficção científica", Apelido = "ficcao" },
            ["acao"] = new() { Nome = "Ação", Apelido = "acao" },
            ["animacao"] = new() { Nome = "Animação", Apelido = "animacao" },
            ["suspense"] = new() { Nome = "Suspense", Apelido = "suspense" },
            ["romance"] = new() { Nome = "Romance", Apelido = "romance" },
            ["documentario"] = new() { Nome = "Documentário", Apelido = "documentario" },
        };

        contexto.Generos.AddRange(generos.Values);

        var pessoas = new Dictionary<string, Pessoa>();

        Pessoa Quem(string nome)
        {
            if (!pessoas.TryGetValue(nome, out var pessoa))
            {
                pessoa = new Pessoa { Nome = nome };
                pessoas[nome] = pessoa;
                contexto.Pessoas.Add(pessoa);
            }

            return pessoa;
        }

        var filmes = new (string Nome, string Sinopse, int Ano, ClassificacaoIndicativa Classificacao, int Duracao,
            string Genero, string Midia, bool Destaque, string[] Elenco, string Direcao)[]
        {
            ("Cidade de Vidro",
                "Numa capital onde todo prédio virou espelho, uma engenheira descobre que os reflexos estão dois segundos atrasados.",
                2024, ClassificacaoIndicativa.Doze, 118, "ficcao", FluxoPadrao, true,
                ["Solange Vieira", "Otávio Braga"], "Rita Amorim"),

            ("O Último Trem para Olinda",
                "Três desconhecidos dividem a última cabine de um trem que ninguém lembra de ter visto sair.",
                2023, ClassificacaoIndicativa.Quatorze, 96, "drama", FluxoApple, false,
                ["Benedito Rangel", "Solange Vieira"], "Rita Amorim"),

            ("Enquanto a Chuva Não Passa",
                "Dois vizinhos que nunca se falaram ficam presos na mesma marquise por uma tarde inteira.",
                2022, ClassificacaoIndicativa.Livre, 84, "romance", FluxoAlternativo, false,
                ["Marina Teles", "Otávio Braga"], "Caio Sampaio"),

            ("Caçadores de Estática",
                "Uma equipe de rádios piratas rastreia um sinal que só aparece durante tempestades.",
                2025, ClassificacaoIndicativa.Dezesseis, 131, "acao", FluxoPadrao, false,
                ["Otávio Braga", "Ivone Castelo"], "Caio Sampaio"),

            ("A Ilha dos Relógios Parados",
                "Uma menina e um farol conversam sobre o que fazer quando o tempo decide tirar férias.",
                2021, ClassificacaoIndicativa.Livre, 77, "animacao", FluxoApple, false,
                ["Marina Teles"], "Lúcia Bandeira"),

            ("Dossiê Meia-Noite",
                "O arquivista de um jornal fechado há trinta anos recebe uma pauta nova.",
                2020, ClassificacaoIndicativa.Dezoito, 104, "suspense", FluxoAlternativo, false,
                ["Benedito Rangel", "Ivone Castelo"], "Rita Amorim"),
        };

        var dia = 0;

        foreach (var filme in filmes)
        {
            var titulo = new Titulo
            {
                Tipo = TipoDeTitulo.Filme,
                Nome = filme.Nome,
                Sinopse = filme.Sinopse,
                Ano = filme.Ano,
                Classificacao = filme.Classificacao,
                DuracaoEmMinutos = filme.Duracao,
                MidiaUrl = filme.Midia,
                Credito = CreditoDaMidia,
                Destaque = filme.Destaque,
                AdicionadoEm = agora.AddDays(-dia++),
                Generos = [generos[filme.Genero]],
            };

            titulo.Participacoes.Add(new Participacao { Pessoa = Quem(filme.Direcao), Papel = Papel.Direcao, Ordem = 0 });

            for (var i = 0; i < filme.Elenco.Length; i += 1)
            {
                titulo.Participacoes.Add(new Participacao
                {
                    Pessoa = Quem(filme.Elenco[i]),
                    Papel = Papel.Elenco,
                    Ordem = i + 1,
                });
            }

            contexto.Titulos.Add(titulo);
        }

        contexto.Titulos.Add(SerieLitoral(generos, Quem, agora.AddDays(-dia++)));
        contexto.Titulos.Add(SerieSinalFraco(generos, Quem, agora.AddDays(-dia)));

        contexto.Perfis.AddRange(
            new Perfil { Nome = "Miguel", Cor = "#A52A45", CriadoEm = agora },
            new Perfil { Nome = "Convidado", Cor = "#541525", CriadoEm = agora },
            new Perfil { Nome = "Infantil", Cor = "#781B32", Infantil = true, CriadoEm = agora });

        contexto.SaveChanges();
    }

    private static Titulo SerieLitoral(
        Dictionary<string, Genero> generos,
        Func<string, Pessoa> quem,
        DateTimeOffset adicionadoEm)
    {
        var serie = new Titulo
        {
            Tipo = TipoDeTitulo.Serie,
            Nome = "Litoral",
            Sinopse = "Uma família administra o último posto de gasolina antes da ponte — e tudo que atravessa por ali.",
            Ano = 2023,
            Classificacao = ClassificacaoIndicativa.Quatorze,
            Credito = CreditoDaMidia,
            AdicionadoEm = adicionadoEm,
            Generos = [generos["drama"]],
        };

        serie.Participacoes.Add(new Participacao { Pessoa = quem("Lúcia Bandeira"), Papel = Papel.Direcao, Ordem = 0 });
        serie.Participacoes.Add(new Participacao { Pessoa = quem("Solange Vieira"), Papel = Papel.Elenco, Ordem = 1 });

        var primeira = new Temporada { Numero = 1, Ano = 2023 };

        var nomes = new[] { "Maré de sizígia", "O turno da madrugada", "Quem paga em espécie", "Ponte levadiça" };

        for (var i = 0; i < nomes.Length; i += 1)
        {
            primeira.Episodios.Add(new Episodio
            {
                Numero = i + 1,
                Nome = nomes[i],
                Sinopse = "Episódio de demonstração do catálogo.",
                DuracaoEmMinutos = 42 + i,
                MidiaUrl = i % 2 == 0 ? FluxoPadrao : FluxoApple,
                AberturaComecaEm = 5,
                AberturaTerminaEm = 35,
            });
        }

        var segunda = new Temporada { Numero = 2, Ano = 2024 };

        foreach (var (nome, i) in new[] { "Vento sul", "O que a ponte engoliu" }.Select((n, i) => (n, i)))
        {
            segunda.Episodios.Add(new Episodio
            {
                Numero = i + 1,
                Nome = nome,
                Sinopse = "Episódio de demonstração do catálogo.",
                DuracaoEmMinutos = 45,
                MidiaUrl = FluxoAlternativo,
                AberturaComecaEm = 5,
                AberturaTerminaEm = 35,
            });
        }

        serie.Temporadas.Add(primeira);
        serie.Temporadas.Add(segunda);

        return serie;
    }

    private static Titulo SerieSinalFraco(
        Dictionary<string, Genero> generos,
        Func<string, Pessoa> quem,
        DateTimeOffset adicionadoEm)
    {
        var serie = new Titulo
        {
            Tipo = TipoDeTitulo.Serie,
            Nome = "Sinal Fraco",
            Sinopse = "Uma operadora de telefonia rural começa a receber chamadas de números que ainda não existem.",
            Ano = 2025,
            Classificacao = ClassificacaoIndicativa.Dezesseis,
            Credito = CreditoDaMidia,
            AdicionadoEm = adicionadoEm,
            Generos = [generos["suspense"], generos["ficcao"]],
        };

        serie.Participacoes.Add(new Participacao { Pessoa = quem("Caio Sampaio"), Papel = Papel.Direcao, Ordem = 0 });
        serie.Participacoes.Add(new Participacao { Pessoa = quem("Ivone Castelo"), Papel = Papel.Elenco, Ordem = 1 });

        var temporada = new Temporada { Numero = 1, Ano = 2025 };

        foreach (var (nome, i) in new[] { "Linha cruzada", "Área de sombra", "Torre 12" }.Select((n, i) => (n, i)))
        {
            temporada.Episodios.Add(new Episodio
            {
                Numero = i + 1,
                Nome = nome,
                Sinopse = "Episódio de demonstração do catálogo.",
                DuracaoEmMinutos = 38 + i,
                MidiaUrl = FluxoPadrao,
            });
        }

        serie.Temporadas.Add(temporada);

        return serie;
    }
}
