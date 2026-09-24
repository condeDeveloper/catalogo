using Catalogo.Core.Armazenamento;
using Catalogo.Core.Consulta;
using Catalogo.Core.Modelo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Api.Endpoints;

/// <summary>A leitura do catálogo: listagem, busca e detalhe.</summary>
public static class CatalogoEndpoints
{
    /// <summary>Registra os endpoints de leitura.</summary>
    public static void MapCatalogo(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var grupo = app.MapGroup("/titulos").WithTags("Catálogo");

        grupo.MapGet("/", Listar)
            .WithSummary("Lista o catálogo, com filtros, busca e paginação por cursor");

        grupo.MapGet("/{id:int}", Detalhe).WithSummary("O título inteiro, com temporadas, episódios e elenco");

        app.MapGet("/generos", async (ContextoDoCatalogo contexto) =>
            Results.Ok(await contexto.Generos
                .OrderBy(g => g.Nome)
                .Select(g => new { g.Id, g.Nome, g.Apelido, titulos = g.Titulos.Count(t => !t.Removido) })
                .ToListAsync()))
            .WithTags("Catálogo")
            .WithSummary("Os gêneros, com quantos títulos cada um tem");
    }

    private static async Task<IResult> Listar(
        ContextoDoCatalogo contexto,
        [FromQuery] string? busca,
        [FromQuery] string? genero,
        [FromQuery] string? tipo,
        [FromQuery] int? anoDe,
        [FromQuery] int? anoAte,
        [FromQuery] string? ordem,
        [FromQuery] string? cursor,
        [FromQuery] int? tamanho,
        [FromQuery] int? perfilId)
    {
        if (tipo is not null && !Enum.TryParse<TipoDeTitulo>(tipo, ignoreCase: true, out _))
        {
            return Results.BadRequest(new { erro = $"Tipo desconhecido: {tipo}. Use Filme ou Serie." });
        }

        if (ordem is not null && !Enum.TryParse<Ordem>(ordem, ignoreCase: true, out _))
        {
            return Results.BadRequest(new { erro = $"Ordem desconhecida: {ordem}. Use Recentes, Alfabetica ou Lancamento." });
        }

        Perfil? perfil = null;

        if (perfilId is { } id)
        {
            perfil = await contexto.Perfis.FindAsync(id);

            if (perfil is null)
            {
                return Results.NotFound(new { erro = $"Perfil {id} não existe." });
            }
        }

        var filtros = new Filtros(
            busca,
            genero,
            tipo is null ? null : Enum.Parse<TipoDeTitulo>(tipo, ignoreCase: true),
            anoDe,
            anoAte,
            ordem is null ? Ordem.Recentes : Enum.Parse<Ordem>(ordem, ignoreCase: true),
            cursor,
            tamanho ?? 24);

        var consulta = ConsultaDeTitulos.Montar(contexto.Titulos.Include(t => t.Generos), filtros, perfil);

        consulta = ConsultaDeTitulos.AplicarCursor(consulta, filtros.Ordem, Cursor.Decodificar(cursor));

        // Pede um a mais do que cabe na página: se ele vier, há mais adiante —
        // e assim o "tem mais" não custa uma segunda consulta de contagem.
        var itens = await consulta.Take(filtros.TamanhoEfetivo + 1).ToListAsync();
        var temMais = itens.Count > filtros.TamanhoEfetivo;

        if (temMais)
        {
            itens.RemoveAt(itens.Count - 1);
        }

        return Results.Ok(new
        {
            itens = itens.Select(Resumo),
            proximoCursor = temMais && itens.Count > 0
                ? ConsultaDeTitulos.CursorDe(itens[^1], filtros.Ordem)
                : null,
        });
    }

    private static async Task<IResult> Detalhe(ContextoDoCatalogo contexto, int id, [FromQuery] int? perfilId)
    {
        var titulo = await contexto.Titulos
            .Include(t => t.Generos)
            .Include(t => t.Participacoes).ThenInclude(p => p.Pessoa)
            .Include(t => t.Temporadas).ThenInclude(t => t.Episodios)
            .FirstOrDefaultAsync(t => t.Id == id && !t.Removido);

        if (titulo is null)
        {
            return Results.NotFound(new { erro = $"Título {id} não existe." });
        }

        if (perfilId is { } dono)
        {
            var perfil = await contexto.Perfis.FindAsync(dono);

            // Um título bloqueado responde 404, e não 403. Dizer "existe mas
            // você não pode ver" já entrega que ele existe — e num perfil
            // infantil isso é exatamente o que não se quer entregar.
            if (perfil is not null && !Core.Perfis.ControleParental.Permite(perfil, titulo))
            {
                return Results.NotFound(new { erro = $"Título {id} não existe." });
            }
        }

        return Results.Ok(new
        {
            titulo.Id,
            tipo = titulo.Tipo.ToString(),
            titulo.Nome,
            titulo.Sinopse,
            titulo.Ano,
            classificacao = titulo.Classificacao.Selo(),
            classificacaoPorExtenso = titulo.Classificacao.PorExtenso(),
            titulo.DuracaoEmMinutos,
            titulo.MidiaUrl,
            titulo.CapaUrl,
            titulo.BannerUrl,
            titulo.Credito,
            generos = titulo.Generos.Select(g => new { g.Nome, g.Apelido }),
            direcao = titulo.Participacoes
                .Where(p => p.Papel == Papel.Direcao)
                .OrderBy(p => p.Ordem)
                .Select(p => p.Pessoa!.Nome),
            elenco = titulo.Participacoes
                .Where(p => p.Papel == Papel.Elenco)
                .OrderBy(p => p.Ordem)
                .Select(p => new { p.Pessoa!.Nome, p.Personagem }),
            temporadas = titulo.Temporadas.OrderBy(t => t.Numero).Select(t => new
            {
                t.Id,
                t.Numero,
                nome = t.NomeParaTela,
                t.Ano,
                episodios = t.Episodios.OrderBy(e => e.Numero).Select(e => new
                {
                    e.Id,
                    e.Numero,
                    e.Nome,
                    e.Sinopse,
                    e.DuracaoEmMinutos,
                    e.MidiaUrl,
                    e.MiniaturaUrl,
                    e.AberturaComecaEm,
                    e.AberturaTerminaEm,
                }),
            }),
        });
    }

    /// <summary>O resumo que vai nas listagens.</summary>
    internal static object Resumo(Titulo titulo) => new
    {
        titulo.Id,
        tipo = titulo.Tipo.ToString(),
        titulo.Nome,
        titulo.Ano,
        classificacao = titulo.Classificacao.Selo(),
        titulo.DuracaoEmMinutos,
        titulo.CapaUrl,
        generos = titulo.Generos.Select(g => g.Apelido),
    };
}
