using Catalogo.Core.Modelo;
using Catalogo.Core.Perfis;

namespace Catalogo.Core.Consulta;

/// <summary>Como ordenar o resultado.</summary>
public enum Ordem
{
    /// <summary>Mais recentes no catálogo primeiro.</summary>
    Recentes = 0,

    /// <summary>Por nome, de A a Z.</summary>
    Alfabetica = 1,

    /// <summary>Do mais novo para o mais antigo, por ano de lançamento.</summary>
    Lancamento = 2,
}

/// <summary>Os filtros de uma busca no catálogo.</summary>
/// <param name="Busca">Texto livre; casa com nome do título e nome de pessoa.</param>
/// <param name="Genero">Apelido do gênero, como "animacao".</param>
/// <param name="Tipo">Filme ou série.</param>
/// <param name="AnoDe">Ano mínimo de lançamento.</param>
/// <param name="AnoAte">Ano máximo de lançamento.</param>
/// <param name="Ordem">Como ordenar.</param>
/// <param name="Cursor">De onde continuar; vem da página anterior.</param>
/// <param name="Tamanho">Quantos itens por página.</param>
public record Filtros(
    string? Busca = null,
    string? Genero = null,
    TipoDeTitulo? Tipo = null,
    int? AnoDe = null,
    int? AnoAte = null,
    Ordem Ordem = Ordem.Recentes,
    string? Cursor = null,
    int Tamanho = 24)
{
    /// <summary>O maior tamanho de página aceito.</summary>
    public const int TamanhoMaximo = 100;

    /// <summary>O tamanho já limitado.</summary>
    public int TamanhoEfetivo => Math.Clamp(Tamanho, 1, TamanhoMaximo);
}

/// <summary>Uma página de resultados.</summary>
/// <typeparam name="T">O que está na página.</typeparam>
/// <param name="Itens">Os itens.</param>
/// <param name="ProximoCursor">O cursor da próxima página, ou nulo no fim.</param>
public record Pagina<T>(IReadOnlyList<T> Itens, string? ProximoCursor)
{
    /// <summary>Se ainda há o que buscar.</summary>
    public bool TemMais => ProximoCursor is not null;
}

/// <summary>
/// A consulta do catálogo.
/// </summary>
/// <remarks>
/// <para>
/// A paginação é por <b>cursor</b>, não por <c>OFFSET</c>. Num catálogo que
/// muda enquanto a pessoa rola a tela, o offset repete e pula itens: entrou um
/// título novo no topo e tudo desceu uma posição, então o item 24 vira o 25 e
/// aparece de novo na página seguinte.
/// </para>
/// <para>
/// O cursor guarda a posição pelo <b>valor</b> da ordenação — o par
/// (chave, id) do último item — e a página seguinte pede "o que vem depois
/// deste par". Insersões antes dele não mudam nada. O <c>id</c> entra no par
/// porque a chave sozinha empata: dois títulos do mesmo ano precisam de um
/// desempate estável, senão a fronteira da página oscila.
/// </para>
/// </remarks>
public static class ConsultaDeTitulos
{
    /// <summary>Monta a consulta, sem executá-la.</summary>
    /// <param name="titulos">A origem.</param>
    /// <param name="filtros">O que filtrar.</param>
    /// <param name="perfil">Quando informado, aplica o controle parental.</param>
    public static IQueryable<Titulo> Montar(IQueryable<Titulo> titulos, Filtros filtros, Perfil? perfil = null)
    {
        ArgumentNullException.ThrowIfNull(titulos);
        ArgumentNullException.ThrowIfNull(filtros);

        var consulta = titulos.Where(t => !t.Removido);

        if (perfil is not null)
        {
            consulta = ControleParental.Filtrar(consulta, perfil);
        }

        if (filtros.Tipo is { } tipo)
        {
            consulta = consulta.Where(t => t.Tipo == tipo);
        }

        if (!string.IsNullOrWhiteSpace(filtros.Genero))
        {
            var apelido = filtros.Genero.Trim().ToLowerInvariant();

            consulta = consulta.Where(t => t.Generos.Any(g => g.Apelido == apelido));
        }

        if (filtros.AnoDe is { } de)
        {
            consulta = consulta.Where(t => t.Ano >= de);
        }

        if (filtros.AnoAte is { } ate)
        {
            consulta = consulta.Where(t => t.Ano <= ate);
        }

        foreach (var termo in Normalizacao.Termos(filtros.Busca))
        {
            // Cada termo vira um filtro próprio, e todos precisam casar. Numa
            // expressão só, "aco grande" exigiria a frase inteira e não acharia
            // "O Grande Aço" — toda inversão de palavras falharia.
            var atual = termo;

            consulta = consulta.Where(t =>
                t.NomeParaBusca.Contains(atual) ||
                t.Participacoes.Any(p => p.Pessoa!.NomeParaBusca.Contains(atual)));
        }

        return Ordenar(consulta, filtros.Ordem);
    }

    /// <summary>Aplica a ordenação, sempre com o id como desempate.</summary>
    public static IQueryable<Titulo> Ordenar(IQueryable<Titulo> consulta, Ordem ordem) => ordem switch
    {
        Ordem.Alfabetica => consulta.OrderBy(t => t.NomeParaBusca).ThenBy(t => t.Id),
        Ordem.Lancamento => consulta.OrderByDescending(t => t.Ano).ThenByDescending(t => t.Id),
        _ => consulta.OrderByDescending(t => t.AdicionadoEm).ThenByDescending(t => t.Id),
    };

    /// <summary>
    /// Aplica o cursor, deixando passar só o que vem depois dele.
    /// </summary>
    /// <remarks>
    /// A comparação é o par completo: ou a chave é estritamente posterior, ou
    /// ela empata e o id é. Comparar só a chave perderia itens empatados na
    /// fronteira da página.
    /// </remarks>
    public static IQueryable<Titulo> AplicarCursor(IQueryable<Titulo> consulta, Ordem ordem, Cursor? cursor)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (cursor is null)
        {
            return consulta;
        }

        var id = cursor.Id;

        return ordem switch
        {
            Ordem.Alfabetica => consulta.Where(t =>
                string.Compare(t.NomeParaBusca, cursor.Texto, StringComparison.Ordinal) > 0 ||
                (t.NomeParaBusca == cursor.Texto && t.Id > id)),

            Ordem.Lancamento => consulta.Where(t => t.Ano < cursor.Numero || (t.Ano == cursor.Numero && t.Id < id)),

            _ => consulta.Where(t =>
                t.AdicionadoEm < cursor.Instante || (t.AdicionadoEm == cursor.Instante && t.Id < id)),
        };
    }

    /// <summary>O cursor do último item de uma página.</summary>
    public static string CursorDe(Titulo titulo, Ordem ordem)
    {
        ArgumentNullException.ThrowIfNull(titulo);

        return ordem switch
        {
            Ordem.Alfabetica => Cursor.Texto_(titulo.NomeParaBusca, titulo.Id).Codificar(),
            Ordem.Lancamento => Cursor.Numero_(titulo.Ano, titulo.Id).Codificar(),
            _ => Cursor.Instante_(titulo.AdicionadoEm, titulo.Id).Codificar(),
        };
    }
}
