using Catalogo.Core.Modelo;
using Catalogo.Core.Perfis;

namespace Catalogo.Core.Inicio;

/// <summary>Um item de fileira, já pronto para a tela.</summary>
/// <param name="TituloId">O título.</param>
/// <param name="Nome">O nome.</param>
/// <param name="Tipo">Filme ou série.</param>
/// <param name="Ano">O ano.</param>
/// <param name="Classificacao">O selo da classificação.</param>
/// <param name="CapaUrl">A arte, quando existe.</param>
/// <param name="Fracao">Quanto da barra preencher, de 0 a 1.</param>
/// <param name="Rotulo">O texto de apoio, quando há.</param>
/// <param name="EpisodioId">O episódio a retomar, quando há.</param>
public record ItemDeFileira(
    int TituloId,
    string Nome,
    TipoDeTitulo Tipo,
    int Ano,
    string Classificacao,
    string? CapaUrl,
    double Fracao = 0,
    string? Rotulo = null,
    int? EpisodioId = null);

/// <summary>Uma fileira da tela inicial.</summary>
/// <param name="Chave">Identificador estável, para a tela guardar a rolagem.</param>
/// <param name="Nome">O cabeçalho da fileira.</param>
/// <param name="Itens">Os itens.</param>
public record Fileira(string Chave, string Nome, IReadOnlyList<ItemDeFileira> Itens);

/// <summary>A tela inicial montada.</summary>
/// <param name="Destaque">O título do topo, quando há um.</param>
/// <param name="Fileiras">As fileiras, na ordem em que aparecem.</param>
public record TelaInicial(ItemDeFileira? Destaque, IReadOnlyList<Fileira> Fileiras);

/// <summary>
/// Monta a tela inicial de um perfil.
/// </summary>
/// <remarks>
/// <para>
/// A ordem das fileiras não é enfeite: é a ordem de quão provável é que a
/// pessoa clique. "Continuar assistindo" vem primeiro porque quem abre o app
/// quase sempre quer terminar o que começou; "minha lista" vem depois porque
/// é uma intenção que a própria pessoa registrou; os gêneros vêm por último
/// porque são descoberta.
/// </para>
/// <para>
/// Duas regras que evitam uma tela esquisita: <b>fileira vazia não aparece</b>
/// — uma faixa com título e nada embaixo parece defeito — e <b>nada se repete
/// entre fileiras</b>, porque ver o mesmo pôster três vezes na mesma tela faz
/// o catálogo parecer menor do que é.
/// </para>
/// </remarks>
public static class MontadorDeInicio
{
    /// <summary>Quantos itens cada fileira mostra.</summary>
    public const int ItensPorFileira = 20;

    /// <summary>Os dados de que a montagem precisa.</summary>
    /// <param name="Perfil">De quem é a tela.</param>
    /// <param name="Progressos">Os progressos do perfil, com título e episódio carregados.</param>
    /// <param name="Lista">A lista do perfil, com título carregado.</param>
    /// <param name="Catalogo">Os títulos visíveis para este perfil.</param>
    public record Entrada(
        Perfil Perfil,
        IReadOnlyList<Progresso> Progressos,
        IReadOnlyList<ItemDaLista> Lista,
        IReadOnlyList<Titulo> Catalogo);

    /// <summary>Monta a tela.</summary>
    public static TelaInicial Montar(Entrada entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var visiveis = ControleParental.Filtrar(entrada.Catalogo.Where(t => !t.Removido), entrada.Perfil).ToList();
        var fileiras = new List<Fileira>();
        var jaApareceu = new HashSet<int>();

        var continuar = ContinuarAssistindo
            .Montar(entrada.Progressos, ItensPorFileira)
            .Where(i => ControleParental.Permite(entrada.Perfil, i.Titulo))
            .Select(i => new ItemDeFileira(
                i.Titulo.Id,
                i.Titulo.Nome,
                i.Titulo.Tipo,
                i.Titulo.Ano,
                i.Titulo.Classificacao.Selo(),
                i.Titulo.CapaUrl,
                i.Fracao,
                i.Rotulo,
                i.Episodio?.Id))
            .ToList();

        Acrescentar(fileiras, jaApareceu, "continuar", "Continuar assistindo", continuar);

        var lista = entrada.Lista
            .Where(i => i.Titulo is not null && !i.Titulo.Removido && ControleParental.Permite(entrada.Perfil, i.Titulo))
            .OrderByDescending(i => i.AdicionadoEm)
            .Take(ItensPorFileira)
            .Select(i => Item(i.Titulo!))
            .ToList();

        Acrescentar(fileiras, jaApareceu, "minha-lista", "Minha lista", lista);

        var lancamentos = visiveis
            .OrderByDescending(t => t.AdicionadoEm)
            .Take(ItensPorFileira)
            .Select(Item)
            .ToList();

        Acrescentar(fileiras, jaApareceu, "lancamentos", "Novidades no catálogo", lancamentos);

        // Os gêneros saem em ordem alfabética para a tela ser estável entre
        // dois carregamentos — uma fileira que troca de lugar a cada F5
        // desorienta mais do que ajuda.
        var porGenero = visiveis
            .SelectMany(t => t.Generos.Select(g => (Genero: g, Titulo: t)))
            .GroupBy(p => p.Genero.Apelido)
            .OrderBy(g => g.First().Genero.Nome, StringComparer.CurrentCulture);

        foreach (var grupo in porGenero)
        {
            var itens = grupo
                .Select(p => p.Titulo)
                .DistinctBy(t => t.Id)
                .OrderByDescending(t => t.Ano)
                .Take(ItensPorFileira)
                .Select(Item)
                .ToList();

            Acrescentar(fileiras, jaApareceu, $"genero-{grupo.Key}", grupo.First().Genero.Nome, itens);
        }

        var destaque = visiveis.FirstOrDefault(t => t.Destaque) ?? visiveis.FirstOrDefault();

        return new TelaInicial(destaque is null ? null : Item(destaque), fileiras);
    }

    private static void Acrescentar(
        List<Fileira> fileiras,
        HashSet<int> jaApareceu,
        string chave,
        string nome,
        IReadOnlyList<ItemDeFileira> itens)
    {
        var novos = itens.Where(i => !jaApareceu.Contains(i.TituloId)).ToList();

        if (novos.Count == 0)
        {
            return;
        }

        foreach (var item in novos)
        {
            jaApareceu.Add(item.TituloId);
        }

        fileiras.Add(new Fileira(chave, nome, novos));
    }

    private static ItemDeFileira Item(Titulo titulo) => new(
        titulo.Id,
        titulo.Nome,
        titulo.Tipo,
        titulo.Ano,
        titulo.Classificacao.Selo(),
        titulo.CapaUrl);
}
