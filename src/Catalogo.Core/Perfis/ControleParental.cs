using Catalogo.Core.Modelo;

namespace Catalogo.Core.Perfis;

/// <summary>
/// O filtro por faixa etária.
/// </summary>
/// <remarks>
/// <para>
/// A regra em si é uma linha: a classificação do título tem que ser menor ou
/// igual à idade do perfil. O que precisa de cuidado é <b>onde</b> ela é
/// aplicada.
/// </para>
/// <para>
/// Se o filtro ficar só na tela inicial, o catálogo vaza por todo lado: pela
/// busca, pela página de gênero, pelo link direto, pela lista salva antes de
/// o perfil virar infantil. Por isso ele mora aqui e é aplicado na
/// <b>consulta</b>, uma vez, e todas as telas passam por ela.
/// </para>
/// </remarks>
public static class ControleParental
{
    /// <summary>Se o perfil pode ver o título.</summary>
    public static bool Permite(Perfil perfil, Titulo titulo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(titulo);

        return titulo.Classificacao.LiberadoPara(perfil.IdadeMaxima);
    }

    /// <summary>Deixa passar só o que o perfil pode ver.</summary>
    public static IQueryable<Titulo> Filtrar(IQueryable<Titulo> consulta, Perfil perfil)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(perfil);

        var idade = perfil.IdadeMaxima;

        // A comparação é pelo valor numérico do enum, que é a idade mínima —
        // é isso que torna a consulta traduzível para SQL e correta ao mesmo
        // tempo.
        return consulta.Where(t => (int)t.Classificacao <= idade);
    }

    /// <summary>O mesmo, para o que já está em memória.</summary>
    public static IEnumerable<Titulo> Filtrar(IEnumerable<Titulo> titulos, Perfil perfil)
    {
        ArgumentNullException.ThrowIfNull(titulos);
        ArgumentNullException.ThrowIfNull(perfil);

        return titulos.Where(t => Permite(perfil, t));
    }
}
