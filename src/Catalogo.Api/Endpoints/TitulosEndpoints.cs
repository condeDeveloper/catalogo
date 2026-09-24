using Catalogo.Core.Armazenamento;
using Catalogo.Core.Modelo;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Api.Endpoints;

/// <summary>O que entra num título ao criar ou alterar.</summary>
/// <param name="Tipo">"Filme" ou "Serie".</param>
/// <param name="Nome">O nome.</param>
/// <param name="Sinopse">A sinopse.</param>
/// <param name="Ano">O ano de lançamento.</param>
/// <param name="Classificacao">O selo: L, 10, 12, 14, 16 ou 18.</param>
/// <param name="DuracaoEmMinutos">Só para filme.</param>
/// <param name="MidiaUrl">Só para filme.</param>
/// <param name="CapaUrl">A arte vertical, opcional.</param>
/// <param name="BannerUrl">A arte horizontal, opcional.</param>
/// <param name="Credito">A quem creditar a mídia.</param>
/// <param name="Destaque">Se aparece no topo da tela inicial.</param>
/// <param name="Generos">Os apelidos dos gêneros.</param>
public record EntradaDeTitulo(
    string Tipo,
    string Nome,
    string? Sinopse,
    int Ano,
    string Classificacao,
    int? DuracaoEmMinutos,
    string? MidiaUrl,
    string? CapaUrl,
    string? BannerUrl,
    string? Credito,
    bool Destaque,
    string[]? Generos);

/// <summary>O que entra numa temporada.</summary>
/// <param name="Numero">O número, a partir de 1.</param>
/// <param name="Nome">O nome, quando há.</param>
/// <param name="Ano">O ano.</param>
public record EntradaDeTemporada(int Numero, string? Nome, int? Ano);

/// <summary>O que entra num episódio.</summary>
/// <param name="Numero">O número dentro da temporada.</param>
/// <param name="Nome">O nome.</param>
/// <param name="Sinopse">A sinopse.</param>
/// <param name="DuracaoEmMinutos">A duração.</param>
/// <param name="MidiaUrl">A playlist HLS.</param>
/// <param name="MiniaturaUrl">A miniatura.</param>
/// <param name="AberturaComecaEm">Onde a abertura começa, em segundos.</param>
/// <param name="AberturaTerminaEm">Onde a abertura termina, em segundos.</param>
public record EntradaDeEpisodio(
    int Numero,
    string Nome,
    string? Sinopse,
    int DuracaoEmMinutos,
    string MidiaUrl,
    string? MiniaturaUrl,
    int? AberturaComecaEm,
    int? AberturaTerminaEm);

/// <summary>A administração do catálogo.</summary>
public static class TitulosEndpoints
{
    /// <summary>Registra o CRUD.</summary>
    public static void MapTitulos(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var grupo = app.MapGroup("/titulos").WithTags("Administração");

        grupo.MapPost("/", Criar).WithSummary("Cria um título");
        grupo.MapPut("/{id:int}", Alterar).WithSummary("Altera um título");
        grupo.MapDelete("/{id:int}", Remover).WithSummary("Tira do catálogo, sem apagar o histórico");
        grupo.MapPost("/{id:int}/restaurar", Restaurar).WithSummary("Traz de volta um título removido");

        grupo.MapPost("/{id:int}/temporadas", CriarTemporada).WithSummary("Cria uma temporada");
        grupo.MapPut("/{id:int}/temporadas/{temporadaId:int}", AlterarTemporada).WithSummary("Altera uma temporada");
        grupo.MapDelete("/{id:int}/temporadas/{temporadaId:int}", RemoverTemporada).WithSummary("Apaga uma temporada");

        grupo.MapPost("/{id:int}/temporadas/{temporadaId:int}/episodios", CriarEpisodio)
            .WithSummary("Cria um episódio");

        grupo.MapPut("/{id:int}/temporadas/{temporadaId:int}/episodios/{episodioId:int}", AlterarEpisodio)
            .WithSummary("Altera um episódio");

        grupo.MapDelete("/{id:int}/temporadas/{temporadaId:int}/episodios/{episodioId:int}", RemoverEpisodio)
            .WithSummary("Apaga um episódio");
    }

    private static async Task<IResult> Criar(ContextoDoCatalogo contexto, EntradaDeTitulo entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var titulo = new Titulo { AdicionadoEm = DateTimeOffset.UtcNow };
        var erro = await Preencher(contexto, titulo, entrada);

        if (erro is not null)
        {
            return erro;
        }

        contexto.Titulos.Add(titulo);
        await contexto.SaveChangesAsync();

        return Results.Created($"/titulos/{titulo.Id}", CatalogoEndpoints.Resumo(titulo));
    }

    private static async Task<IResult> Alterar(ContextoDoCatalogo contexto, int id, EntradaDeTitulo entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var titulo = await contexto.Titulos.Include(t => t.Generos).FirstOrDefaultAsync(t => t.Id == id);

        if (titulo is null)
        {
            return Results.NotFound(new { erro = $"Título {id} não existe." });
        }

        var erro = await Preencher(contexto, titulo, entrada);

        if (erro is not null)
        {
            return erro;
        }

        await contexto.SaveChangesAsync();

        return Results.Ok(CatalogoEndpoints.Resumo(titulo));
    }

    /// <summary>Copia a entrada para a entidade, validando.</summary>
    private static async Task<IResult?> Preencher(ContextoDoCatalogo contexto, Titulo titulo, EntradaDeTitulo entrada)
    {
        if (!Enum.TryParse<TipoDeTitulo>(entrada.Tipo, ignoreCase: true, out var tipo))
        {
            return Results.BadRequest(new { erro = $"Tipo desconhecido: {entrada.Tipo}. Use Filme ou Serie." });
        }

        ClassificacaoIndicativa classificacao;

        try
        {
            classificacao = Classificacoes.DoSelo(entrada.Classificacao);
        }
        catch (ArgumentException excecao)
        {
            return Results.BadRequest(new { erro = excecao.Message });
        }

        titulo.Tipo = tipo;
        titulo.Nome = entrada.Nome;
        titulo.Sinopse = entrada.Sinopse ?? string.Empty;
        titulo.Ano = entrada.Ano;
        titulo.Classificacao = classificacao;
        titulo.DuracaoEmMinutos = entrada.DuracaoEmMinutos;
        titulo.MidiaUrl = entrada.MidiaUrl;
        titulo.CapaUrl = entrada.CapaUrl;
        titulo.BannerUrl = entrada.BannerUrl;
        titulo.Credito = entrada.Credito;
        titulo.Destaque = entrada.Destaque;

        if (entrada.Generos is { Length: > 0 })
        {
            var apelidos = entrada.Generos.Select(g => g.Trim().ToLowerInvariant()).ToArray();
            var achados = await contexto.Generos.Where(g => apelidos.Contains(g.Apelido)).ToListAsync();
            var faltando = apelidos.Except(achados.Select(g => g.Apelido)).ToArray();

            if (faltando.Length > 0)
            {
                return Results.BadRequest(new { erro = $"Gênero inexistente: {string.Join(", ", faltando)}." });
            }

            titulo.Generos.Clear();
            titulo.Generos.AddRange(achados);
        }

        try
        {
            titulo.Validar();
        }
        catch (ArgumentException excecao)
        {
            return Results.BadRequest(new { erro = excecao.Message });
        }

        return null;
    }

    private static async Task<IResult> Remover(ContextoDoCatalogo contexto, int id)
    {
        var titulo = await contexto.Titulos.FindAsync(id);

        if (titulo is null)
        {
            return Results.NotFound(new { erro = $"Título {id} não existe." });
        }

        // Remoção lógica: o progresso de quem estava assistindo e as
        // avaliações continuam de pé, e o título pode voltar depois.
        titulo.Removido = true;
        await contexto.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> Restaurar(ContextoDoCatalogo contexto, int id)
    {
        var titulo = await contexto.Titulos.FindAsync(id);

        if (titulo is null)
        {
            return Results.NotFound(new { erro = $"Título {id} não existe." });
        }

        titulo.Removido = false;
        await contexto.SaveChangesAsync();

        return Results.Ok(CatalogoEndpoints.Resumo(titulo));
    }

    private static async Task<IResult> CriarTemporada(ContextoDoCatalogo contexto, int id, EntradaDeTemporada entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var titulo = await contexto.Titulos.Include(t => t.Temporadas).FirstOrDefaultAsync(t => t.Id == id);

        if (titulo is null)
        {
            return Results.NotFound(new { erro = $"Título {id} não existe." });
        }

        if (titulo.Tipo != TipoDeTitulo.Serie)
        {
            return Results.BadRequest(new { erro = "Só série tem temporada." });
        }

        if (entrada.Numero < 1)
        {
            return Results.BadRequest(new { erro = "A temporada começa em 1." });
        }

        if (titulo.Temporadas.Any(t => t.Numero == entrada.Numero))
        {
            return Results.Conflict(new { erro = $"A temporada {entrada.Numero} já existe." });
        }

        var temporada = new Temporada { Numero = entrada.Numero, Nome = entrada.Nome, Ano = entrada.Ano };

        titulo.Temporadas.Add(temporada);
        await contexto.SaveChangesAsync();

        return Results.Created($"/titulos/{id}/temporadas/{temporada.Id}", new
        {
            temporada.Id,
            temporada.Numero,
            nome = temporada.NomeParaTela,
            temporada.Ano,
        });
    }

    private static async Task<IResult> AlterarTemporada(
        ContextoDoCatalogo contexto,
        int id,
        int temporadaId,
        EntradaDeTemporada entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var temporada = await contexto.Temporadas.FirstOrDefaultAsync(t => t.Id == temporadaId && t.TituloId == id);

        if (temporada is null)
        {
            return Results.NotFound(new { erro = $"Temporada {temporadaId} não existe no título {id}." });
        }

        temporada.Numero = entrada.Numero;
        temporada.Nome = entrada.Nome;
        temporada.Ano = entrada.Ano;

        await contexto.SaveChangesAsync();

        return Results.Ok(new { temporada.Id, temporada.Numero, nome = temporada.NomeParaTela, temporada.Ano });
    }

    private static async Task<IResult> RemoverTemporada(ContextoDoCatalogo contexto, int id, int temporadaId)
    {
        var temporada = await contexto.Temporadas.FirstOrDefaultAsync(t => t.Id == temporadaId && t.TituloId == id);

        if (temporada is null)
        {
            return Results.NotFound(new { erro = $"Temporada {temporadaId} não existe no título {id}." });
        }

        contexto.Temporadas.Remove(temporada);
        await contexto.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> CriarEpisodio(
        ContextoDoCatalogo contexto,
        int id,
        int temporadaId,
        EntradaDeEpisodio entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var temporada = await contexto.Temporadas
            .Include(t => t.Episodios)
            .FirstOrDefaultAsync(t => t.Id == temporadaId && t.TituloId == id);

        if (temporada is null)
        {
            return Results.NotFound(new { erro = $"Temporada {temporadaId} não existe no título {id}." });
        }

        if (entrada.Numero < 1 || entrada.DuracaoEmMinutos < 1 || string.IsNullOrWhiteSpace(entrada.MidiaUrl))
        {
            return Results.BadRequest(new { erro = "Episódio precisa de número, duração e mídia." });
        }

        if (temporada.Episodios.Any(e => e.Numero == entrada.Numero))
        {
            return Results.Conflict(new { erro = $"O episódio {entrada.Numero} já existe nesta temporada." });
        }

        var episodio = new Episodio
        {
            Numero = entrada.Numero,
            Nome = entrada.Nome,
            Sinopse = entrada.Sinopse ?? string.Empty,
            DuracaoEmMinutos = entrada.DuracaoEmMinutos,
            MidiaUrl = entrada.MidiaUrl,
            MiniaturaUrl = entrada.MiniaturaUrl,
            AberturaComecaEm = entrada.AberturaComecaEm,
            AberturaTerminaEm = entrada.AberturaTerminaEm,
        };

        temporada.Episodios.Add(episodio);
        await contexto.SaveChangesAsync();

        return Results.Created($"/titulos/{id}/temporadas/{temporadaId}/episodios/{episodio.Id}", new
        {
            episodio.Id,
            episodio.Numero,
            episodio.Nome,
            episodio.DuracaoEmMinutos,
        });
    }

    private static async Task<IResult> AlterarEpisodio(
        ContextoDoCatalogo contexto,
        int id,
        int temporadaId,
        int episodioId,
        EntradaDeEpisodio entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var episodio = await contexto.Episodios
            .Include(e => e.Temporada)
            .FirstOrDefaultAsync(e => e.Id == episodioId && e.TemporadaId == temporadaId && e.Temporada!.TituloId == id);

        if (episodio is null)
        {
            return Results.NotFound(new { erro = $"Episódio {episodioId} não existe." });
        }

        episodio.Numero = entrada.Numero;
        episodio.Nome = entrada.Nome;
        episodio.Sinopse = entrada.Sinopse ?? string.Empty;
        episodio.DuracaoEmMinutos = entrada.DuracaoEmMinutos;
        episodio.MidiaUrl = entrada.MidiaUrl;
        episodio.MiniaturaUrl = entrada.MiniaturaUrl;
        episodio.AberturaComecaEm = entrada.AberturaComecaEm;
        episodio.AberturaTerminaEm = entrada.AberturaTerminaEm;

        await contexto.SaveChangesAsync();

        return Results.Ok(new { episodio.Id, episodio.Numero, episodio.Nome, episodio.DuracaoEmMinutos });
    }

    private static async Task<IResult> RemoverEpisodio(
        ContextoDoCatalogo contexto,
        int id,
        int temporadaId,
        int episodioId)
    {
        var episodio = await contexto.Episodios
            .Include(e => e.Temporada)
            .FirstOrDefaultAsync(e => e.Id == episodioId && e.TemporadaId == temporadaId && e.Temporada!.TituloId == id);

        if (episodio is null)
        {
            return Results.NotFound(new { erro = $"Episódio {episodioId} não existe." });
        }

        contexto.Episodios.Remove(episodio);
        await contexto.SaveChangesAsync();

        return Results.NoContent();
    }
}
