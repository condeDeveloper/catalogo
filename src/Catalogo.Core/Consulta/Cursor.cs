using System.Globalization;
using System.Text;

namespace Catalogo.Core.Consulta;

/// <summary>
/// A posição de onde continuar uma listagem.
/// </summary>
/// <remarks>
/// <para>
/// Guarda o par (chave da ordenação, id). A chave muda conforme a ordem
/// escolhida — um texto, um número ou um instante —, e o id é o desempate
/// estável que impede a fronteira da página de oscilar entre itens iguais.
/// </para>
/// <para>
/// Vai codificado em base64url por um motivo prático: um cursor que parece um
/// valor convida quem consome a API a montá-lo na mão, e aí qualquer mudança
/// na ordenação vira quebra de contrato. Opaco, ele só pode ser devolvido
/// como veio.
/// </para>
/// </remarks>
public sealed record Cursor
{
    private Cursor(char tipo, string chave, int id)
    {
        Tipo = tipo;
        Chave = chave;
        Id = id;
    }

    /// <summary>Que tipo de chave este cursor carrega.</summary>
    public char Tipo { get; }

    /// <summary>A chave, em texto.</summary>
    public string Chave { get; }

    /// <summary>O id do último item da página anterior.</summary>
    public int Id { get; }

    /// <summary>A chave como texto.</summary>
    public string Texto => Chave;

    /// <summary>A chave como número.</summary>
    public int Numero => int.TryParse(Chave, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : 0;

    /// <summary>A chave como instante.</summary>
    public DateTimeOffset Instante =>
        DateTimeOffset.TryParse(Chave, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var valor)
            ? valor
            : DateTimeOffset.MinValue;

    /// <summary>Um cursor com chave de texto.</summary>
    public static Cursor Texto_(string chave, int id) => new('t', chave, id);

    /// <summary>Um cursor com chave numérica.</summary>
    public static Cursor Numero_(int chave, int id) => new('n', chave.ToString(CultureInfo.InvariantCulture), id);

    /// <summary>Um cursor com chave de instante.</summary>
    public static Cursor Instante_(DateTimeOffset chave, int id) =>
        new('i', chave.ToString("O", CultureInfo.InvariantCulture), id);

    /// <summary>Codifica em base64url.</summary>
    public string Codificar()
    {
        var bruto = $"{Tipo}|{Id}|{Chave}";
        var bytes = Encoding.UTF8.GetBytes(bruto);

        // Base64url, e não base64: o cursor viaja na barra de endereços, e
        // "+" e "/" ali seriam lidos como espaço e como separador de caminho.
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Lê um cursor de volta.
    /// </summary>
    /// <remarks>
    /// Cursor inválido devolve <c>null</c> em vez de lançar. Ele chega da URL,
    /// onde qualquer coisa pode ser digitada, e começar a listagem do início é
    /// uma resposta melhor do que um 500 — ou do que um 400 que derruba a
    /// rolagem infinita da tela.
    /// </remarks>
    public static Cursor? Decodificar(string? codificado)
    {
        if (string.IsNullOrWhiteSpace(codificado))
        {
            return null;
        }

        try
        {
            var normalizado = codificado.Replace('-', '+').Replace('_', '/');
            var bruto = Encoding.UTF8.GetString(Convert.FromBase64String(normalizado.PadRight(
                normalizado.Length + ((4 - (normalizado.Length % 4)) % 4), '=')));

            var partes = bruto.Split('|', 3);

            if (partes.Length != 3 || partes[0].Length != 1)
            {
                return null;
            }

            if (!int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return null;
            }

            return new Cursor(partes[0][0], partes[2], id);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
