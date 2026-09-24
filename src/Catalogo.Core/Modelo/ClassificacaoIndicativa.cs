namespace Catalogo.Core.Modelo;

/// <summary>
/// A classificação indicativa brasileira, do Ministério da Justiça.
/// </summary>
/// <remarks>
/// <para>
/// São seis faixas: Livre, 10, 12, 14, 16 e 18 anos. O detalhe que estraga
/// implementações apressadas é que <b>elas não ordenam pelo texto</b>:
/// alfabeticamente, "10" vem antes de "L", e "18" vem antes de "L" também.
/// Quem guarda a classificação como texto e ordena com <c>ORDER BY</c> entrega
/// um controle parental que deixa passar filme de 18 anos para criança.
/// </para>
/// <para>
/// Por isso o valor guardado é a <b>idade mínima</b> — 0, 10, 12, 14, 16, 18 —
/// que ordena sozinha e compara sozinha. O texto é só apresentação.
/// </para>
/// </remarks>
public enum ClassificacaoIndicativa
{
    /// <summary>Livre para todos os públicos.</summary>
    Livre = 0,

    /// <summary>Não recomendado para menores de 10 anos.</summary>
    Dez = 10,

    /// <summary>Não recomendado para menores de 12 anos.</summary>
    Doze = 12,

    /// <summary>Não recomendado para menores de 14 anos.</summary>
    Quatorze = 14,

    /// <summary>Não recomendado para menores de 16 anos.</summary>
    Dezesseis = 16,

    /// <summary>Não recomendado para menores de 18 anos.</summary>
    Dezoito = 18,
}

/// <summary>Conversões e apresentação da classificação indicativa.</summary>
public static class Classificacoes
{
    /// <summary>Todas as faixas, da mais livre para a mais restrita.</summary>
    public static readonly IReadOnlyList<ClassificacaoIndicativa> Todas =
    [
        ClassificacaoIndicativa.Livre,
        ClassificacaoIndicativa.Dez,
        ClassificacaoIndicativa.Doze,
        ClassificacaoIndicativa.Quatorze,
        ClassificacaoIndicativa.Dezesseis,
        ClassificacaoIndicativa.Dezoito,
    ];

    /// <summary>A idade mínima recomendada, em anos.</summary>
    public static int IdadeMinima(this ClassificacaoIndicativa classificacao) => (int)classificacao;

    /// <summary>O selo como aparece na tela: "L", "10", "12"…</summary>
    public static string Selo(this ClassificacaoIndicativa classificacao) =>
        classificacao == ClassificacaoIndicativa.Livre ? "L" : ((int)classificacao).ToString();

    /// <summary>A frase completa, para a tela de detalhes.</summary>
    public static string PorExtenso(this ClassificacaoIndicativa classificacao) =>
        classificacao == ClassificacaoIndicativa.Livre
            ? "Livre para todos os públicos"
            : $"Não recomendado para menores de {(int)classificacao} anos";

    /// <summary>
    /// Lê o selo de volta, aceitando "L", "l", "livre" e os números.
    /// </summary>
    /// <remarks>
    /// Recusa o que não conhece em vez de cair na faixa mais livre: um valor
    /// desconhecido virando <c>Livre</c> é justamente o erro que libera
    /// conteúdo adulto num perfil infantil.
    /// </remarks>
    public static ClassificacaoIndicativa DoSelo(string selo)
    {
        ArgumentNullException.ThrowIfNull(selo);

        var limpo = selo.Trim();

        if (limpo.Equals("L", StringComparison.OrdinalIgnoreCase) ||
            limpo.Equals("livre", StringComparison.OrdinalIgnoreCase))
        {
            return ClassificacaoIndicativa.Livre;
        }

        if (int.TryParse(limpo, out var idade) && Todas.Any(c => (int)c == idade))
        {
            return (ClassificacaoIndicativa)idade;
        }

        throw new ArgumentException(
            $"Classificação desconhecida: \"{selo}\". Use L, 10, 12, 14, 16 ou 18.",
            nameof(selo));
    }

    /// <summary>Indica se quem tem esta idade pode assistir.</summary>
    public static bool LiberadoPara(this ClassificacaoIndicativa classificacao, int idade) =>
        idade >= (int)classificacao;
}
