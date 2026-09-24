namespace Catalogo.Core.Modelo;

/// <summary>Um gênero do catálogo.</summary>
public class Genero
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>O nome, como "Animação".</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>O identificador na URL, como "animacao".</summary>
    public string Apelido { get; set; } = string.Empty;

    /// <summary>Os títulos deste gênero.</summary>
    public List<Titulo> Titulos { get; set; } = [];
}
