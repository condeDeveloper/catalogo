using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalogo.Core.Armazenamento;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Catalogo.Tests;

/// <summary>
/// A API de ponta a ponta, por HTTP de verdade.
/// </summary>
/// <remarks>
/// <para>
/// O banco é SQLite <b>em memória</b>, com a conexão mantida aberta pela
/// fábrica. Fechá-la apagaria o banco inteiro — no SQLite em memória, o banco
/// vive enquanto a conexão viver, e cada teste recomeçaria do zero no meio da
/// própria execução.
/// </para>
/// <para>
/// A troca é feita nos serviços, e não na configuração: com hospedagem
/// mínima, o que o <c>WebApplicationFactory</c> injeta em configuração não é
/// visto pelas leituras que acontecem antes do <c>Build()</c>.
/// </para>
/// </remarks>
public sealed class Fabrica : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _conexao = new("DataSource=:memory:");

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder construtor)
    {
        ArgumentNullException.ThrowIfNull(construtor);

        _conexao.Open();

        construtor.UseEnvironment("Development");

        construtor.ConfigureServices(servicos =>
        {
            var registro = servicos.Single(s => s.ServiceType == typeof(DbContextOptions<ContextoDoCatalogo>));

            servicos.Remove(registro);
            servicos.AddDbContext<ContextoDoCatalogo>(opcoes => opcoes.UseSqlite(_conexao));
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool descartando)
    {
        base.Dispose(descartando);

        if (descartando)
        {
            _conexao.Dispose();
        }
    }
}

/// <summary>Os testes da API.</summary>
public class ApiTest(Fabrica fabrica) : IClassFixture<Fabrica>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "a API sobe com o catálogo de demonstração semeado")]
    public async Task SobeComSemente()
    {
        var saude = await _cliente.GetFromJsonAsync<JsonElement>("/saude");

        saude.GetProperty("ok").GetBoolean().Should().BeTrue();
        saude.GetProperty("titulos").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "a listagem devolve itens e um cursor")]
    public async Task ListagemComCursor()
    {
        var primeira = await _cliente.GetFromJsonAsync<JsonElement>("/titulos?tamanho=3");
        var itens = primeira.GetProperty("itens").EnumerateArray().ToList();

        itens.Should().HaveCount(3);

        var cursor = primeira.GetProperty("proximoCursor").GetString();

        cursor.Should().NotBeNullOrEmpty();

        var segunda = await _cliente.GetFromJsonAsync<JsonElement>($"/titulos?tamanho=3&cursor={cursor}");
        var idsDaPrimeira = itens.Select(i => i.GetProperty("id").GetInt32()).ToHashSet();
        var idsDaSegunda = segunda.GetProperty("itens").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();

        // A segunda página não repete nada da primeira — que é justamente o
        // que o OFFSET não garante quando o catálogo muda no meio.
        idsDaSegunda.Should().NotIntersectWith(idsDaPrimeira);
    }

    [Fact(DisplayName = "a paginação percorre o catálogo inteiro, sem repetir nem pular")]
    public async Task PaginacaoCobreTudo()
    {
        var todos = new List<int>();
        string? cursor = null;

        for (var pagina = 0; pagina < 20; pagina += 1)
        {
            var url = cursor is null ? "/titulos?tamanho=2" : $"/titulos?tamanho=2&cursor={cursor}";
            var resposta = await _cliente.GetFromJsonAsync<JsonElement>(url);

            todos.AddRange(resposta.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));

            cursor = resposta.GetProperty("proximoCursor").GetString();

            if (cursor is null)
            {
                break;
            }
        }

        todos.Should().OnlyHaveUniqueItems();

        var total = (await _cliente.GetFromJsonAsync<JsonElement>("/saude")).GetProperty("titulos").GetInt32();

        todos.Should().HaveCount(total);
    }

    [Fact(DisplayName = "a busca ignora acento")]
    public async Task BuscaIgnoraAcento()
    {
        var comAcento = await _cliente.GetFromJsonAsync<JsonElement>("/titulos?busca=Relógios");
        var semAcento = await _cliente.GetFromJsonAsync<JsonElement>("/titulos?busca=relogios");

        semAcento.GetProperty("itens").GetArrayLength().Should().Be(comAcento.GetProperty("itens").GetArrayLength());
        semAcento.GetProperty("itens").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "a busca acha pelo nome de quem dirigiu")]
    public async Task BuscaPorPessoa()
    {
        var resposta = await _cliente.GetFromJsonAsync<JsonElement>("/titulos?busca=Rita Amorim");

        resposta.GetProperty("itens").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "filtro de tipo e de gênero desconhecido é recusado com clareza")]
    public async Task FiltrosInvalidos()
    {
        var tipo = await _cliente.GetAsync("/titulos?tipo=Documentario");

        tipo.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ordem = await _cliente.GetAsync("/titulos?ordem=Aleatoria");

        ordem.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "o ciclo completo de um título: criar, ler, alterar, remover e restaurar")]
    public async Task CicloDoTitulo()
    {
        var criacao = await _cliente.PostAsJsonAsync("/titulos", new
        {
            tipo = "Filme",
            nome = "Travessia de Inverno",
            sinopse = "Um cartógrafo redesenha uma cidade que só existe quando neva.",
            ano = 2026,
            classificacao = "12",
            duracaoEmMinutos = 101,
            midiaUrl = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8",
            destaque = false,
            generos = new[] { "drama" },
        });

        criacao.StatusCode.Should().Be(HttpStatusCode.Created);

        var criado = await criacao.Content.ReadFromJsonAsync<JsonElement>(Opcoes);
        var id = criado.GetProperty("id").GetInt32();

        var detalhe = await _cliente.GetFromJsonAsync<JsonElement>($"/titulos/{id}");

        detalhe.GetProperty("nome").GetString().Should().Be("Travessia de Inverno");
        detalhe.GetProperty("classificacao").GetString().Should().Be("12");
        detalhe.GetProperty("classificacaoPorExtenso").GetString().Should().Contain("12 anos");

        var alteracao = await _cliente.PutAsJsonAsync($"/titulos/{id}", new
        {
            tipo = "Filme",
            nome = "Travessia de Inverno (versão do diretor)",
            sinopse = "Idem, com vinte minutos a mais.",
            ano = 2026,
            classificacao = "14",
            duracaoEmMinutos = 121,
            midiaUrl = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8",
            destaque = false,
            generos = new[] { "drama" },
        });

        alteracao.StatusCode.Should().Be(HttpStatusCode.OK);

        var remocao = await _cliente.DeleteAsync($"/titulos/{id}");

        remocao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _cliente.GetAsync($"/titulos/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var restauracao = await _cliente.PostAsync($"/titulos/{id}/restaurar", null);

        restauracao.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _cliente.GetAsync($"/titulos/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        await _cliente.DeleteAsync($"/titulos/{id}");
    }

    [Fact(DisplayName = "título incoerente é recusado com o motivo")]
    public async Task TituloIncoerente()
    {
        var semDuracao = await _cliente.PostAsJsonAsync("/titulos", new
        {
            tipo = "Filme",
            nome = "Sem duração",
            ano = 2026,
            classificacao = "L",
            midiaUrl = "https://exemplo/a.m3u8",
            destaque = false,
        });

        semDuracao.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await semDuracao.Content.ReadAsStringAsync()).Should().Contain("duração");

        var classificacaoInventada = await _cliente.PostAsJsonAsync("/titulos", new
        {
            tipo = "Filme",
            nome = "Classificação errada",
            ano = 2026,
            classificacao = "PG-13",
            duracaoEmMinutos = 90,
            midiaUrl = "https://exemplo/a.m3u8",
            destaque = false,
        });

        classificacaoInventada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "temporada e episódio seguem o ciclo, e o número não repete")]
    public async Task CicloDaSerie()
    {
        var criacao = await _cliente.PostAsJsonAsync("/titulos", new
        {
            tipo = "Serie",
            nome = "Estação Rodoviária",
            sinopse = "Histórias que só acontecem entre uma plataforma e outra.",
            ano = 2026,
            classificacao = "10",
            destaque = false,
            generos = new[] { "drama" },
        });

        var serieId = (await criacao.Content.ReadFromJsonAsync<JsonElement>(Opcoes)).GetProperty("id").GetInt32();

        var temporada = await _cliente.PostAsJsonAsync($"/titulos/{serieId}/temporadas", new { numero = 1, ano = 2026 });

        temporada.StatusCode.Should().Be(HttpStatusCode.Created);

        var temporadaId = (await temporada.Content.ReadFromJsonAsync<JsonElement>(Opcoes)).GetProperty("id").GetInt32();

        var repetida = await _cliente.PostAsJsonAsync($"/titulos/{serieId}/temporadas", new { numero = 1, ano = 2026 });

        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var episodio = await _cliente.PostAsJsonAsync(
            $"/titulos/{serieId}/temporadas/{temporadaId}/episodios",
            new
            {
                numero = 1,
                nome = "Plataforma 4",
                duracaoEmMinutos = 38,
                midiaUrl = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8",
            });

        episodio.StatusCode.Should().Be(HttpStatusCode.Created);

        var detalhe = await _cliente.GetFromJsonAsync<JsonElement>($"/titulos/{serieId}");

        detalhe.GetProperty("temporadas").GetArrayLength().Should().Be(1);
        detalhe.GetProperty("temporadas")[0].GetProperty("episodios").GetArrayLength().Should().Be(1);

        await _cliente.DeleteAsync($"/titulos/{serieId}");
    }

    [Fact(DisplayName = "filme não aceita temporada")]
    public async Task FilmeNaoTemTemporada()
    {
        var filmes = await _cliente.GetFromJsonAsync<JsonElement>("/titulos?tipo=Filme&tamanho=1");
        var id = filmes.GetProperty("itens")[0].GetProperty("id").GetInt32();

        var resposta = await _cliente.PostAsJsonAsync($"/titulos/{id}/temporadas", new { numero = 1 });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "a lista aceita clique duplo sem duplicar")]
    public async Task ListaAceitaCliqueDuplo()
    {
        var perfilId = await PrimeiroPerfil();
        var tituloId = await PrimeiroTitulo();

        await _cliente.PostAsync($"/perfis/{perfilId}/lista/{tituloId}", null);
        await _cliente.PostAsync($"/perfis/{perfilId}/lista/{tituloId}", null);

        var lista = await _cliente.GetFromJsonAsync<JsonElement>($"/perfis/{perfilId}/lista");

        lista.EnumerateArray().Count(i => i.GetProperty("id").GetInt32() == tituloId).Should().Be(1);

        await _cliente.DeleteAsync($"/perfis/{perfilId}/lista/{tituloId}");

        var depois = await _cliente.GetFromJsonAsync<JsonElement>($"/perfis/{perfilId}/lista");

        depois.EnumerateArray().Should().NotContain(i => i.GetProperty("id").GetInt32() == tituloId);
    }

    [Fact(DisplayName = "o progresso é limitado à duração da mídia")]
    public async Task ProgressoNaoPassaDaDuracao()
    {
        // O player relata a posição a cada poucos segundos, e um relatório
        // atrasado pode chegar com um valor maior do que a mídia inteira.
        var perfilId = await PrimeiroPerfil();
        var tituloId = await PrimeiroTitulo();

        var resposta = await _cliente.PutAsJsonAsync($"/perfis/{perfilId}/progresso", new
        {
            tituloId,
            segundoAtual = 999_999,
            duracaoEmSegundos = 7080,
        });

        var gravado = await resposta.Content.ReadFromJsonAsync<JsonElement>(Opcoes);

        gravado.GetProperty("segundoAtual").GetInt32().Should().Be(7080);
        gravado.GetProperty("concluido").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "o que está no meio aparece em continuar assistindo")]
    public async Task ContinuarAssistindoAparece()
    {
        var perfilId = await PrimeiroPerfil();
        var tituloId = await PrimeiroTitulo();

        await _cliente.PutAsJsonAsync($"/perfis/{perfilId}/progresso", new
        {
            tituloId,
            segundoAtual = 3540,
            duracaoEmSegundos = 7080,
        });

        var inicio = await _cliente.GetFromJsonAsync<JsonElement>($"/inicio/{perfilId}");
        var fileiras = inicio.GetProperty("fileiras").EnumerateArray().ToList();
        var continuar = fileiras.FirstOrDefault(f => f.GetProperty("chave").GetString() == "continuar");

        continuar.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        continuar.GetProperty("itens")[0].GetProperty("fracao").GetDouble().Should().BeApproximately(0.5, 0.01);
    }

    [Fact(DisplayName = "a tela do perfil infantil não traz título adulto")]
    public async Task TelaInfantilFiltra()
    {
        var perfis = await _cliente.GetFromJsonAsync<JsonElement>("/perfis");
        var infantil = perfis.EnumerateArray().First(p => p.GetProperty("infantil").GetBoolean());
        var id = infantil.GetProperty("id").GetInt32();

        var inicio = await _cliente.GetFromJsonAsync<JsonElement>($"/inicio/{id}");

        var nomes = inicio.GetProperty("fileiras").EnumerateArray()
            .SelectMany(f => f.GetProperty("itens").EnumerateArray())
            .Select(i => i.GetProperty("classificacao").GetString())
            .ToList();

        nomes.Should().NotBeEmpty();
        nomes.Should().OnlyContain(c => c == "L" || c == "10");
    }

    [Fact(DisplayName = "título bloqueado responde 404, e não 403")]
    public async Task BloqueadoResponde404()
    {
        // Dizer "existe mas você não pode ver" já entrega que ele existe.
        var perfis = await _cliente.GetFromJsonAsync<JsonElement>("/perfis");
        var infantil = perfis.EnumerateArray().First(p => p.GetProperty("infantil").GetBoolean());
        var perfilId = infantil.GetProperty("id").GetInt32();

        var adultos = await _cliente.GetFromJsonAsync<JsonElement>("/titulos?busca=Dossiê");
        var id = adultos.GetProperty("itens")[0].GetProperty("id").GetInt32();

        var resposta = await _cliente.GetAsync($"/titulos/{id}?perfilId={perfilId}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "a avaliação é substituída, não acumulada")]
    public async Task AvaliacaoSubstitui()
    {
        var perfilId = await PrimeiroPerfil();
        var tituloId = await PrimeiroTitulo();

        await _cliente.PutAsJsonAsync($"/perfis/{perfilId}/avaliacoes", new { tituloId, nota = "Gostei" });
        await _cliente.PutAsJsonAsync($"/perfis/{perfilId}/avaliacoes", new { tituloId, nota = "Amei" });

        var avaliacoes = await _cliente.GetFromJsonAsync<JsonElement>($"/perfis/{perfilId}/avaliacoes");
        var doTitulo = avaliacoes.EnumerateArray().Where(a => a.GetProperty("tituloId").GetInt32() == tituloId).ToList();

        doTitulo.Should().ContainSingle();
        doTitulo[0].GetProperty("nota").GetString().Should().Be("Amei");

        await _cliente.DeleteAsync($"/perfis/{perfilId}/avaliacoes/{tituloId}");
    }

    [Fact(DisplayName = "nota inventada é recusada")]
    public async Task NotaInventada()
    {
        var perfilId = await PrimeiroPerfil();
        var tituloId = await PrimeiroTitulo();

        var resposta = await _cliente.PutAsJsonAsync(
            $"/perfis/{perfilId}/avaliacoes",
            new { tituloId, nota = "CincoEstrelas" });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "o perfil segue o ciclo, e o último não pode ser apagado")]
    public async Task CicloDoPerfil()
    {
        var criacao = await _cliente.PostAsJsonAsync("/perfis", new { nome = "Visitante", cor = "#3B0F1C", infantil = false });

        criacao.StatusCode.Should().Be(HttpStatusCode.Created);

        var id = (await criacao.Content.ReadFromJsonAsync<JsonElement>(Opcoes)).GetProperty("id").GetInt32();

        var alteracao = await _cliente.PutAsJsonAsync($"/perfis/{id}", new { nome = "Visitante 2", infantil = true, limiteDeIdade = (int?)null });

        alteracao.StatusCode.Should().Be(HttpStatusCode.OK);
        (await alteracao.Content.ReadFromJsonAsync<JsonElement>(Opcoes)).GetProperty("idadeMaxima").GetInt32().Should().Be(10);

        (await _cliente.DeleteAsync($"/perfis/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "perfil e título inexistentes respondem 404")]
    public async Task InexistentesRespondem404()
    {
        (await _cliente.GetAsync("/titulos/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _cliente.GetAsync("/inicio/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _cliente.GetAsync("/perfis/999999/lista")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> PrimeiroPerfil()
    {
        var perfis = await _cliente.GetFromJsonAsync<JsonElement>("/perfis");

        return perfis.EnumerateArray().First(p => !p.GetProperty("infantil").GetBoolean()).GetProperty("id").GetInt32();
    }

    private async Task<int> PrimeiroTitulo()
    {
        var titulos = await _cliente.GetFromJsonAsync<JsonElement>("/titulos?tipo=Filme&tamanho=1");

        return titulos.GetProperty("itens")[0].GetProperty("id").GetInt32();
    }
}
