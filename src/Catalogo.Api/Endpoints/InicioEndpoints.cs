using Catalogo.Core.Armazenamento;
using Catalogo.Core.Inicio;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Api.Endpoints;

/// <summary>A tela inicial de um perfil.</summary>
public static class InicioEndpoints
{
    /// <summary>Registra o endpoint da tela inicial.</summary>
    public static void MapInicio(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/inicio/{perfilId:int}", async (ContextoDoCatalogo contexto, int perfilId) =>
        {
            var perfil = await contexto.Perfis.FindAsync(perfilId);

            if (perfil is null)
            {
                return Results.NotFound(new { erro = $"Perfil {perfilId} não existe." });
            }

            // Uma consulta por conjunto, e a montagem em memória. A alternativa
            // — uma consulta por fileira — multiplica idas ao banco por algo
            // que cabe todo numa tela.
            var progressos = await contexto.Progressos
                .Where(p => p.PerfilId == perfilId)
                .Include(p => p.Titulo).ThenInclude(t => t!.Temporadas).ThenInclude(t => t.Episodios)
                .Include(p => p.Episodio)
                .ToListAsync();

            var lista = await contexto.ItensDaLista
                .Where(i => i.PerfilId == perfilId)
                .Include(i => i.Titulo)
                .ToListAsync();

            var catalogo = await contexto.Titulos
                .Where(t => !t.Removido)
                .Include(t => t.Generos)
                .ToListAsync();

            var tela = MontadorDeInicio.Montar(new MontadorDeInicio.Entrada(perfil, progressos, lista, catalogo));

            return Results.Ok(new
            {
                perfil = new { perfil.Id, perfil.Nome, perfil.Cor, perfil.Infantil, perfil.IdadeMaxima },
                destaque = tela.Destaque,
                fileiras = tela.Fileiras,
            });
        })
        .WithTags("Tela inicial")
        .WithSummary("Monta a tela inicial: destaque e fileiras, já filtradas pelo controle parental");
    }
}
