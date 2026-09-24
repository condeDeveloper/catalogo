using Catalogo.Core.Armazenamento;
using Catalogo.Core.Modelo;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Api.Endpoints;

/// <summary>O que entra num perfil.</summary>
/// <param name="Nome">O nome.</param>
/// <param name="Cor">A cor do avatar, em hexadecimal.</param>
/// <param name="Infantil">Se é perfil infantil.</param>
/// <param name="LimiteDeIdade">O limite explícito, quando há.</param>
public record EntradaDePerfil(string Nome, string? Cor, bool Infantil, int? LimiteDeIdade);

/// <summary>Onde a reprodução parou.</summary>
/// <param name="TituloId">O título.</param>
/// <param name="EpisodioId">O episódio, quando é série.</param>
/// <param name="SegundoAtual">Em que segundo parou.</param>
/// <param name="DuracaoEmSegundos">A duração da mídia.</param>
public record EntradaDeProgresso(int TituloId, int? EpisodioId, int SegundoAtual, int DuracaoEmSegundos);

/// <summary>Uma avaliação.</summary>
/// <param name="TituloId">O título.</param>
/// <param name="Nota">"Amei", "Gostei" ou "NaoGostei".</param>
public record EntradaDeAvaliacao(int TituloId, string Nota);

/// <summary>Perfis, lista, progresso e avaliações.</summary>
public static class PerfisEndpoints
{
    /// <summary>Registra os endpoints de perfil.</summary>
    public static void MapPerfis(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var grupo = app.MapGroup("/perfis").WithTags("Perfis");

        grupo.MapGet("/", async (ContextoDoCatalogo contexto) => Results.Ok(
            await contexto.Perfis.OrderBy(p => p.CriadoEm).Select(p => new
            {
                p.Id,
                p.Nome,
                p.Cor,
                p.Infantil,
                idadeMaxima = p.Infantil ? (p.LimiteDeIdade ?? 10) : (p.LimiteDeIdade ?? 18),
            }).ToListAsync()));

        grupo.MapPost("/", Criar);
        grupo.MapPut("/{id:int}", Alterar);
        grupo.MapDelete("/{id:int}", Remover);

        grupo.MapGet("/{id:int}/lista", VerLista);
        grupo.MapPost("/{id:int}/lista/{tituloId:int}", Marcar);
        grupo.MapDelete("/{id:int}/lista/{tituloId:int}", Desmarcar);

        grupo.MapGet("/{id:int}/progresso", VerProgresso);
        grupo.MapPut("/{id:int}/progresso", GravarProgresso);

        grupo.MapGet("/{id:int}/avaliacoes", VerAvaliacoes);
        grupo.MapPut("/{id:int}/avaliacoes", Avaliar);
        grupo.MapDelete("/{id:int}/avaliacoes/{tituloId:int}", Desavaliar);
    }

    private static async Task<IResult> Criar(ContextoDoCatalogo contexto, EntradaDePerfil entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        if (string.IsNullOrWhiteSpace(entrada.Nome))
        {
            return Results.BadRequest(new { erro = "O perfil precisa de nome." });
        }

        var perfil = new Perfil
        {
            Nome = entrada.Nome.Trim(),
            Cor = entrada.Cor ?? "#A52A45",
            Infantil = entrada.Infantil,
            LimiteDeIdade = entrada.LimiteDeIdade,
            CriadoEm = DateTimeOffset.UtcNow,
        };

        contexto.Perfis.Add(perfil);
        await contexto.SaveChangesAsync();

        return Results.Created($"/perfis/{perfil.Id}", new { perfil.Id, perfil.Nome, perfil.Cor, perfil.Infantil });
    }

    private static async Task<IResult> Alterar(ContextoDoCatalogo contexto, int id, EntradaDePerfil entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var perfil = await contexto.Perfis.FindAsync(id);

        if (perfil is null)
        {
            return Results.NotFound(new { erro = $"Perfil {id} não existe." });
        }

        perfil.Nome = entrada.Nome.Trim();
        perfil.Cor = entrada.Cor ?? perfil.Cor;
        perfil.Infantil = entrada.Infantil;
        perfil.LimiteDeIdade = entrada.LimiteDeIdade;

        await contexto.SaveChangesAsync();

        return Results.Ok(new { perfil.Id, perfil.Nome, perfil.Cor, perfil.Infantil, perfil.IdadeMaxima });
    }

    private static async Task<IResult> Remover(ContextoDoCatalogo contexto, int id)
    {
        var perfil = await contexto.Perfis.FindAsync(id);

        if (perfil is null)
        {
            return Results.NotFound(new { erro = $"Perfil {id} não existe." });
        }

        if (await contexto.Perfis.CountAsync() == 1)
        {
            // Uma conta sem perfil nenhum não tem tela para mostrar. Barrar
            // aqui é mais honesto do que deixar a interface quebrar depois.
            return Results.BadRequest(new { erro = "A conta precisa de pelo menos um perfil." });
        }

        // Aqui a remoção é física, e de propósito: o que some é o histórico
        // pessoal de alguém, e guardar isso depois de a pessoa pedir para
        // apagar seria o comportamento errado.
        contexto.Progressos.RemoveRange(contexto.Progressos.Where(p => p.PerfilId == id));
        contexto.ItensDaLista.RemoveRange(contexto.ItensDaLista.Where(i => i.PerfilId == id));
        contexto.Avaliacoes.RemoveRange(contexto.Avaliacoes.Where(a => a.PerfilId == id));
        contexto.Perfis.Remove(perfil);

        await contexto.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> VerLista(ContextoDoCatalogo contexto, int id)
    {
        if (!await contexto.Perfis.AnyAsync(p => p.Id == id))
        {
            return Results.NotFound(new { erro = $"Perfil {id} não existe." });
        }

        var itens = await contexto.ItensDaLista
            .Where(i => i.PerfilId == id && !i.Titulo!.Removido)
            .Include(i => i.Titulo).ThenInclude(t => t!.Generos)
            .OrderByDescending(i => i.AdicionadoEm)
            .ToListAsync();

        return Results.Ok(itens.Select(i => CatalogoEndpoints.Resumo(i.Titulo!)));
    }

    private static async Task<IResult> Marcar(ContextoDoCatalogo contexto, int id, int tituloId)
    {
        if (!await contexto.Perfis.AnyAsync(p => p.Id == id) ||
            !await contexto.Titulos.AnyAsync(t => t.Id == tituloId && !t.Removido))
        {
            return Results.NotFound(new { erro = "Perfil ou título não existe." });
        }

        // Marcar duas vezes não é erro: é clique duplo. Responder 200 com o
        // estado final deixa a tela simples — ela não precisa saber se já
        // estava lá.
        if (!await contexto.ItensDaLista.AnyAsync(i => i.PerfilId == id && i.TituloId == tituloId))
        {
            contexto.ItensDaLista.Add(new ItemDaLista
            {
                PerfilId = id,
                TituloId = tituloId,
                AdicionadoEm = DateTimeOffset.UtcNow,
            });

            await contexto.SaveChangesAsync();
        }

        return Results.Ok(new { naLista = true });
    }

    private static async Task<IResult> Desmarcar(ContextoDoCatalogo contexto, int id, int tituloId)
    {
        var item = await contexto.ItensDaLista.FirstOrDefaultAsync(i => i.PerfilId == id && i.TituloId == tituloId);

        if (item is not null)
        {
            contexto.ItensDaLista.Remove(item);
            await contexto.SaveChangesAsync();
        }

        return Results.Ok(new { naLista = false });
    }

    private static async Task<IResult> VerProgresso(ContextoDoCatalogo contexto, int id)
    {
        if (!await contexto.Perfis.AnyAsync(p => p.Id == id))
        {
            return Results.NotFound(new { erro = $"Perfil {id} não existe." });
        }

        var progressos = await contexto.Progressos
            .Where(p => p.PerfilId == id)
            .OrderByDescending(p => p.AtualizadoEm)
            .ToListAsync();

        return Results.Ok(progressos.Select(p => new
        {
            p.TituloId,
            p.EpisodioId,
            p.SegundoAtual,
            p.DuracaoEmSegundos,
            fracao = Math.Round(p.Fracao, 4),
            concluido = Core.Perfis.ContinuarAssistindo.Concluido(p),
            p.AtualizadoEm,
        }));
    }

    private static async Task<IResult> GravarProgresso(ContextoDoCatalogo contexto, int id, EntradaDeProgresso entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        if (!await contexto.Perfis.AnyAsync(p => p.Id == id))
        {
            return Results.NotFound(new { erro = $"Perfil {id} não existe." });
        }

        if (!await contexto.Titulos.AnyAsync(t => t.Id == entrada.TituloId))
        {
            return Results.NotFound(new { erro = $"Título {entrada.TituloId} não existe." });
        }

        if (entrada.DuracaoEmSegundos <= 0 || entrada.SegundoAtual < 0)
        {
            return Results.BadRequest(new { erro = "Duração precisa ser positiva e o segundo atual não pode ser negativo." });
        }

        var progresso = await contexto.Progressos.FirstOrDefaultAsync(p =>
            p.PerfilId == id && p.TituloId == entrada.TituloId && p.EpisodioId == entrada.EpisodioId);

        if (progresso is null)
        {
            progresso = new Progresso
            {
                PerfilId = id,
                TituloId = entrada.TituloId,
                EpisodioId = entrada.EpisodioId,
            };

            contexto.Progressos.Add(progresso);
        }

        // O segundo é limitado à duração: o player manda a posição a cada
        // poucos segundos, e um relatório atrasado pode chegar com um valor
        // maior do que a mídia inteira.
        progresso.SegundoAtual = Math.Min(entrada.SegundoAtual, entrada.DuracaoEmSegundos);
        progresso.DuracaoEmSegundos = entrada.DuracaoEmSegundos;
        progresso.AtualizadoEm = DateTimeOffset.UtcNow;

        await contexto.SaveChangesAsync();

        return Results.Ok(new
        {
            progresso.TituloId,
            progresso.EpisodioId,
            progresso.SegundoAtual,
            fracao = Math.Round(progresso.Fracao, 4),
            concluido = Core.Perfis.ContinuarAssistindo.Concluido(progresso),
        });
    }

    private static async Task<IResult> VerAvaliacoes(ContextoDoCatalogo contexto, int id)
    {
        if (!await contexto.Perfis.AnyAsync(p => p.Id == id))
        {
            return Results.NotFound(new { erro = $"Perfil {id} não existe." });
        }

        var avaliacoes = await contexto.Avaliacoes.Where(a => a.PerfilId == id).ToListAsync();

        return Results.Ok(avaliacoes.Select(a => new { a.TituloId, nota = a.Nota.ToString(), a.AvaliadoEm }));
    }

    private static async Task<IResult> Avaliar(ContextoDoCatalogo contexto, int id, EntradaDeAvaliacao entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        if (!Enum.TryParse<Nota>(entrada.Nota, ignoreCase: true, out var nota))
        {
            return Results.BadRequest(new { erro = $"Nota desconhecida: {entrada.Nota}. Use Amei, Gostei ou NaoGostei." });
        }

        if (!await contexto.Perfis.AnyAsync(p => p.Id == id) ||
            !await contexto.Titulos.AnyAsync(t => t.Id == entrada.TituloId))
        {
            return Results.NotFound(new { erro = "Perfil ou título não existe." });
        }

        var avaliacao = await contexto.Avaliacoes
            .FirstOrDefaultAsync(a => a.PerfilId == id && a.TituloId == entrada.TituloId);

        if (avaliacao is null)
        {
            avaliacao = new Avaliacao { PerfilId = id, TituloId = entrada.TituloId };
            contexto.Avaliacoes.Add(avaliacao);
        }

        avaliacao.Nota = nota;
        avaliacao.AvaliadoEm = DateTimeOffset.UtcNow;

        await contexto.SaveChangesAsync();

        return Results.Ok(new { avaliacao.TituloId, nota = avaliacao.Nota.ToString() });
    }

    private static async Task<IResult> Desavaliar(ContextoDoCatalogo contexto, int id, int tituloId)
    {
        var avaliacao = await contexto.Avaliacoes.FirstOrDefaultAsync(a => a.PerfilId == id && a.TituloId == tituloId);

        if (avaliacao is not null)
        {
            contexto.Avaliacoes.Remove(avaliacao);
            await contexto.SaveChangesAsync();
        }

        return Results.NoContent();
    }
}
