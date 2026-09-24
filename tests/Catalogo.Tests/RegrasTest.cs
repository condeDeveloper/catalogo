using Xunit;

using Catalogo.Core.Consulta;
using Catalogo.Core.Inicio;
using Catalogo.Core.Modelo;
using Catalogo.Core.Perfis;
using FluentAssertions;

namespace Catalogo.Tests;

/// <summary>As regras que não dependem de banco nem de HTTP.</summary>
public class RegrasTest
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------- classificação

    [Fact(DisplayName = "a classificação ordena por idade, não por texto")]
    public void ClassificacaoOrdenaPorIdade()
    {
        // Alfabeticamente, "10" e "18" vêm antes de "L" — e um controle
        // parental que ordena texto libera filme adulto para criança.
        var selos = Classificacoes.Todas.Select(c => c.Selo()).ToArray();

        selos.Should().ContainInOrder("L", "10", "12", "14", "16", "18");
        selos.OrderBy(s => s, StringComparer.Ordinal).Should().NotEqual(selos);

        Classificacoes.Todas.Select(c => c.IdadeMinima()).Should().BeInAscendingOrder();
    }

    [Theory(DisplayName = "o selo vai e volta")]
    [InlineData("L", ClassificacaoIndicativa.Livre)]
    [InlineData("l", ClassificacaoIndicativa.Livre)]
    [InlineData("livre", ClassificacaoIndicativa.Livre)]
    [InlineData("10", ClassificacaoIndicativa.Dez)]
    [InlineData(" 18 ", ClassificacaoIndicativa.Dezoito)]
    public void SeloVaiEVolta(string selo, ClassificacaoIndicativa esperada) =>
        Classificacoes.DoSelo(selo).Should().Be(esperada);

    [Theory(DisplayName = "selo desconhecido é recusado, e não vira Livre")]
    [InlineData("13")]
    [InlineData("PG-13")]
    [InlineData("")]
    [InlineData("qualquer")]
    public void SeloDesconhecidoERecusado(string selo)
    {
        // Cair em Livre por desconhecimento é exatamente o erro que libera
        // conteúdo adulto num perfil infantil.
        var acao = () => Classificacoes.DoSelo(selo);

        acao.Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------- controle parental

    [Fact(DisplayName = "o perfil infantil só vê o que a idade permite")]
    public void PerfilInfantilFiltra()
    {
        var infantil = new Perfil { Nome = "Criança", Infantil = true };
        var adulto = new Perfil { Nome = "Adulto" };

        infantil.IdadeMaxima.Should().Be(10);
        adulto.IdadeMaxima.Should().Be(18);

        var catalogo = Classificacoes.Todas
            .Select(c => new Titulo { Nome = c.Selo(), Classificacao = c })
            .ToList();

        ControleParental.Filtrar(catalogo, infantil).Select(t => t.Nome).Should().Equal("L", "10");
        ControleParental.Filtrar(catalogo, adulto).Should().HaveCount(6);
    }

    [Fact(DisplayName = "o limite explícito manda sobre o sinalizador infantil")]
    public void LimiteExplicitoManda()
    {
        var perfil = new Perfil { Nome = "Adolescente", LimiteDeIdade = 14 };
        var catorze = new Titulo { Classificacao = ClassificacaoIndicativa.Quatorze };
        var dezesseis = new Titulo { Classificacao = ClassificacaoIndicativa.Dezesseis };

        ControleParental.Permite(perfil, catorze).Should().BeTrue();
        ControleParental.Permite(perfil, dezesseis).Should().BeFalse();
    }

    // ------------------------------------------------------- normalização

    [Theory(DisplayName = "a busca ignora acento e caixa")]
    [InlineData("Coração", "coracao")]
    [InlineData("SINTEL", "sintel")]
    [InlineData("  Água   Viva  ", "agua viva")]
    [InlineData("Ñandú", "nandu")]
    [InlineData(null, "")]
    public void NormalizacaoTiraAcento(string? entrada, string esperado) =>
        Normalizacao.Normalizar(entrada).Should().Be(esperado);

    [Fact(DisplayName = "cada termo da busca vale sozinho")]
    public void TermosSaoIndependentes() =>
        Normalizacao.Termos("  Grande   AÇO ").Should().Equal("grande", "aco");

    // ------------------------------------------------------------ cursor

    [Fact(DisplayName = "o cursor vai e volta")]
    public void CursorVaiEVolta()
    {
        var instante = Cursor.Instante_(Agora, 42);
        var lido = Cursor.Decodificar(instante.Codificar());

        lido.Should().NotBeNull();
        lido!.Id.Should().Be(42);
        lido.Instante.Should().Be(Agora);
    }

    [Fact(DisplayName = "o cursor é base64url, sem + nem / nem =")]
    public void CursorEBase64Url()
    {
        // Ele viaja na barra de endereços: "+" vira espaço e "/" vira
        // separador de caminho.
        var codificado = Cursor.Texto_("um nome qualquer com ção", 7).Codificar();

        codificado.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
        Cursor.Decodificar(codificado)!.Texto.Should().Be("um nome qualquer com ção");
    }

    [Theory(DisplayName = "cursor inválido volta nulo, em vez de derrubar a listagem")]
    [InlineData("")]
    [InlineData("!!!não é base64!!!")]
    [InlineData("YWJj")]
    public void CursorInvalidoVoltaNulo(string entrada) => Cursor.Decodificar(entrada).Should().BeNull();

    // -------------------------------------------------- continuar assistindo

    [Fact(DisplayName = "espiada de dez segundos não entra na fileira")]
    public void EspiadaNaoEntra()
    {
        var progresso = new Progresso { SegundoAtual = 10, DuracaoEmSegundos = 7200 };

        ContinuarAssistindo.EmAndamento(progresso).Should().BeFalse();
    }

    [Fact(DisplayName = "em conteúdo curto vale o piso em segundos, não a fração")]
    public void ConteudoCurtoUsaPiso()
    {
        // Num episódio de 3 minutos, 2% são menos de 4 segundos — tempo de
        // errar o clique.
        var curto = new Progresso { SegundoAtual = 40, DuracaoEmSegundos = 180 };

        ContinuarAssistindo.EmAndamento(curto).Should().BeTrue();
    }

    [Fact(DisplayName = "quem chegou ao fim sai da fileira")]
    public void ConcluidoSai()
    {
        var quaseNoFim = new Progresso { SegundoAtual = 6700, DuracaoEmSegundos = 7200 };

        ContinuarAssistindo.Concluido(quaseNoFim).Should().BeTrue();
        ContinuarAssistindo.EmAndamento(quaseNoFim).Should().BeFalse();
    }

    [Fact(DisplayName = "o filme no meio aparece com a barra preenchida")]
    public void FilmeNoMeioAparece()
    {
        var filme = Filme("Cidade de Vidro", 118);
        var progresso = new Progresso
        {
            Titulo = filme,
            TituloId = filme.Id,
            SegundoAtual = 3540,
            DuracaoEmSegundos = 7080,
            AtualizadoEm = Agora,
        };

        var fileira = ContinuarAssistindo.Montar([progresso]);

        fileira.Should().ContainSingle();
        fileira[0].Titulo.Should().Be(filme);
        fileira[0].Fracao.Should().BeApproximately(0.5, 0.001);
        fileira[0].Rotulo.Should().Contain("Faltam");
    }

    [Fact(DisplayName = "episódio terminado leva ao próximo, no segundo zero")]
    public void EpisodioTerminadoLevaAoProximo()
    {
        var serie = Serie();
        var primeiro = serie.Temporadas[0].Episodios[0];

        var progresso = new Progresso
        {
            Titulo = serie,
            TituloId = serie.Id,
            Episodio = primeiro,
            EpisodioId = primeiro.Id,
            SegundoAtual = 2500,
            DuracaoEmSegundos = 2520,
            AtualizadoEm = Agora,
        };

        var fileira = ContinuarAssistindo.Montar([progresso]);

        fileira.Should().ContainSingle();
        fileira[0].Episodio!.Numero.Should().Be(2);
        fileira[0].SegundoAtual.Should().Be(0);
        fileira[0].Rotulo.Should().StartWith("T1:E2");
    }

    [Fact(DisplayName = "o último episódio de uma temporada leva ao primeiro da seguinte")]
    public void ViradaDeTemporada()
    {
        // Sem isso, a série some da fileira no fim de cada temporada —
        // justamente quando a pessoa mais provavelmente vai continuar.
        var serie = Serie();
        var ultimoDaPrimeira = serie.Temporadas[0].Episodios[^1];

        var proximo = ContinuarAssistindo.ProximoEpisodio(serie, ultimoDaPrimeira);

        proximo.Should().NotBeNull();
        proximo!.Numero.Should().Be(1);
        proximo.Temporada!.Numero.Should().Be(2);
    }

    [Fact(DisplayName = "o último episódio da série não leva a lugar nenhum")]
    public void FimDaSerie()
    {
        var serie = Serie();
        var ultimo = serie.Temporadas[^1].Episodios[^1];

        ContinuarAssistindo.ProximoEpisodio(serie, ultimo).Should().BeNull();
        ContinuarAssistindo.Montar([new Progresso
        {
            Titulo = serie,
            TituloId = serie.Id,
            Episodio = ultimo,
            EpisodioId = ultimo.Id,
            SegundoAtual = 2500,
            DuracaoEmSegundos = 2520,
            AtualizadoEm = Agora,
        }]).Should().BeEmpty();
    }

    [Fact(DisplayName = "a série aparece uma vez só, mesmo com vários episódios começados")]
    public void SerieApareceUmaVez()
    {
        var serie = Serie();
        var progressos = serie.Temporadas[0].Episodios.Take(3).Select((e, i) => new Progresso
        {
            Titulo = serie,
            TituloId = serie.Id,
            Episodio = e,
            EpisodioId = e.Id,
            SegundoAtual = 600,
            DuracaoEmSegundos = 2520,
            AtualizadoEm = Agora.AddMinutes(i),
        }).ToList();

        var fileira = ContinuarAssistindo.Montar(progressos);

        fileira.Should().ContainSingle();
        fileira[0].Episodio!.Numero.Should().Be(3, "o mais recente é o que vale");
    }

    [Fact(DisplayName = "título removido do catálogo some da fileira")]
    public void RemovidoSome()
    {
        var filme = Filme("Saiu do ar", 100);

        filme.Removido = true;

        ContinuarAssistindo.Montar([new Progresso
        {
            Titulo = filme,
            TituloId = filme.Id,
            SegundoAtual = 1000,
            DuracaoEmSegundos = 6000,
            AtualizadoEm = Agora,
        }]).Should().BeEmpty();
    }

    // ---------------------------------------------------- tela inicial

    [Fact(DisplayName = "fileira vazia não aparece na tela")]
    public void FileiraVaziaNaoAparece()
    {
        var perfil = new Perfil { Nome = "Alguém" };
        var tela = MontadorDeInicio.Montar(new MontadorDeInicio.Entrada(perfil, [], [], [Filme("Único", 90)]));

        tela.Fileiras.Should().OnlyContain(f => f.Itens.Count > 0);
        tela.Fileiras.Should().NotContain(f => f.Chave == "continuar");
        tela.Fileiras.Should().NotContain(f => f.Chave == "minha-lista");
    }

    [Fact(DisplayName = "o mesmo título não aparece em duas fileiras")]
    public void SemRepeticaoEntreFileiras()
    {
        // Ver o mesmo pôster três vezes na mesma tela faz o catálogo parecer
        // menor do que é.
        var perfil = new Perfil { Nome = "Alguém" };
        var genero = new Genero { Nome = "Drama", Apelido = "drama" };
        var filme = Filme("Repetido", 90);

        filme.Generos.Add(genero);

        var lista = new List<ItemDaLista> { new() { Titulo = filme, TituloId = filme.Id, AdicionadoEm = Agora } };
        var tela = MontadorDeInicio.Montar(new MontadorDeInicio.Entrada(perfil, [], lista, [filme]));

        tela.Fileiras.SelectMany(f => f.Itens).Should().ContainSingle(i => i.TituloId == filme.Id);
    }

    [Fact(DisplayName = "a tela do perfil infantil não traz o que ele não pode ver")]
    public void TelaInfantilFiltra()
    {
        var perfil = new Perfil { Nome = "Criança", Infantil = true };

        var livre = Filme("Para todos", 80);
        var adulto = Filme("Só adulto", 120);

        adulto.Classificacao = ClassificacaoIndicativa.Dezoito;

        var tela = MontadorDeInicio.Montar(new MontadorDeInicio.Entrada(perfil, [], [], [livre, adulto]));

        tela.Fileiras.SelectMany(f => f.Itens).Should().NotContain(i => i.Nome == "Só adulto");
        tela.Destaque!.Nome.Should().Be("Para todos");
    }

    // ---------------------------------------------------------- auxiliares

    private static int _proximoId = 1;

    private static Titulo Filme(string nome, int minutos) => new()
    {
        Id = _proximoId++,
        Tipo = TipoDeTitulo.Filme,
        Nome = nome,
        Ano = 2024,
        Classificacao = ClassificacaoIndicativa.Livre,
        DuracaoEmMinutos = minutos,
        MidiaUrl = "https://exemplo/playlist.m3u8",
        AdicionadoEm = Agora,
    };

    private static Titulo Serie()
    {
        var serie = new Titulo
        {
            Id = _proximoId++,
            Tipo = TipoDeTitulo.Serie,
            Nome = "Litoral",
            Ano = 2023,
            Classificacao = ClassificacaoIndicativa.Livre,
            AdicionadoEm = Agora,
        };

        foreach (var numeroDaTemporada in new[] { 1, 2 })
        {
            var temporada = new Temporada { Id = _proximoId++, Numero = numeroDaTemporada, Titulo = serie };

            foreach (var numero in new[] { 1, 2, 3 })
            {
                temporada.Episodios.Add(new Episodio
                {
                    Id = _proximoId++,
                    Numero = numero,
                    Nome = $"Episódio {numero}",
                    DuracaoEmMinutos = 42,
                    MidiaUrl = "https://exemplo/playlist.m3u8",
                    Temporada = temporada,
                });
            }

            serie.Temporadas.Add(temporada);
        }

        return serie;
    }
}
