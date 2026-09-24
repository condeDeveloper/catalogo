namespace Catalogo.Core.Modelo;

/// <summary>Um episódio de uma temporada.</summary>
public class Episodio
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>A temporada a que pertence.</summary>
    public int TemporadaId { get; set; }

    /// <summary>A temporada a que pertence.</summary>
    public Temporada? Temporada { get; set; }

    /// <summary>O número dentro da temporada, começando em 1.</summary>
    public int Numero { get; set; }

    /// <summary>O nome do episódio.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>A sinopse.</summary>
    public string Sinopse { get; set; } = string.Empty;

    /// <summary>A duração, em minutos.</summary>
    public int DuracaoEmMinutos { get; set; }

    /// <summary>A playlist HLS.</summary>
    public string MidiaUrl { get; set; } = string.Empty;

    /// <summary>A miniatura.</summary>
    public string? MiniaturaUrl { get; set; }

    /// <summary>
    /// Onde a abertura começa e termina, em segundos.
    /// </summary>
    /// <remarks>
    /// É o que faz existir o botão "pular abertura". Guardar como dois
    /// números no episódio é o suficiente: detectar a abertura sozinho exige
    /// comparar o áudio entre episódios, que é outro projeto inteiro.
    /// </remarks>
    public int? AberturaComecaEm { get; set; }

    /// <summary>Onde a abertura termina, em segundos.</summary>
    public int? AberturaTerminaEm { get; set; }

    /// <summary>O código curto, como "T1:E3".</summary>
    public string Codigo => $"T{Temporada?.Numero ?? 0}:E{Numero}";
}
