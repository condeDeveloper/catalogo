using System.Globalization;
using System.Text;

namespace Catalogo.Core.Consulta;

/// <summary>
/// A normalização usada pela busca.
/// </summary>
/// <remarks>
/// <para>
/// Quem digita "coracao" espera achar "Coração", e quem digita "SINTEL"
/// espera achar "Sintel". O SQLite não faz nem uma coisa nem outra sozinho: o
/// <c>LIKE</c> dele ignora maiúsculas apenas em ASCII e não conhece acento
/// nenhum.
/// </para>
/// <para>
/// A saída é guardar uma coluna já normalizada e comparar normalizado com
/// normalizado. Fazer isso na consulta, com função, funcionaria e impediria o
/// uso de índice — o que num catálogo de milhares de títulos é a diferença
/// entre buscar em 1 ms e em 300.
/// </para>
/// </remarks>
public static class Normalizacao
{
    /// <summary>
    /// Tira acento, baixa a caixa e junta os espaços.
    /// </summary>
    /// <remarks>
    /// A decomposição em <c>FormD</c> separa a letra do acento; descartar as
    /// marcas que não ocupam espaço deixa a letra sozinha. É o jeito que
    /// funciona para qualquer acento, em vez de uma tabela de substituição
    /// que esquece o "ñ" e o "ü".
    /// </remarks>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var construtor = new StringBuilder(decomposto.Length);
        var espacoPendente = false;

        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(caractere))
            {
                espacoPendente = construtor.Length > 0;
                continue;
            }

            if (espacoPendente)
            {
                construtor.Append(' ');
                espacoPendente = false;
            }

            construtor.Append(caractere);
        }

        return construtor.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Quebra a busca em termos.
    /// </summary>
    /// <remarks>
    /// Cada termo precisa aparecer, em qualquer ordem: quem digita "aco
    /// grande" acha "O Grande Aço". Exigir a frase inteira faria a busca
    /// falhar em toda inversão.
    /// </remarks>
    public static IReadOnlyList<string> Termos(string? busca)
    {
        var normalizado = Normalizar(busca);

        return normalizado.Length == 0
            ? []
            : normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
