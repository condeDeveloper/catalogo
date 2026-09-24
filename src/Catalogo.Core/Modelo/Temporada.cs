namespace Catalogo.Core.Modelo;

/// <summary>Uma temporada de uma série.</summary>
public class Temporada
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>A série a que pertence.</summary>
    public int TituloId { get; set; }

    /// <summary>A série a que pertence.</summary>
    public Titulo? Titulo { get; set; }

    /// <summary>O número, começando em 1.</summary>
    public int Numero { get; set; }

    /// <summary>
    /// O nome, quando a temporada tem um.
    /// </summary>
    /// <remarks>
    /// Série antológica dá nome a cada temporada; a maioria não dá. Por isso
    /// é opcional, e a tela cai para "Temporada N" quando falta.
    /// </remarks>
    public string? Nome { get; set; }

    /// <summary>O ano em que foi ao ar.</summary>
    public int? Ano { get; set; }

    /// <summary>Os episódios, em ordem.</summary>
    public List<Episodio> Episodios { get; set; } = [];

    /// <summary>O nome a mostrar na tela.</summary>
    public string NomeParaTela => Nome ?? $"Temporada {Numero}";
}
