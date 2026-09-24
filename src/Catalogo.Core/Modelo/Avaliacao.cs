namespace Catalogo.Core.Modelo;

/// <summary>O que o perfil achou.</summary>
public enum Nota
{
    /// <summary>Não gostei.</summary>
    NaoGostei = -1,

    /// <summary>Gostei.</summary>
    Gostei = 1,

    /// <summary>Amei.</summary>
    Amei = 2,
}

/// <summary>
/// A avaliação de um título por um perfil.
/// </summary>
/// <remarks>
/// Três valores em vez de cinco estrelas. A troca é conhecida: estrela dá uma
/// nota média que ninguém sabe interpretar, e polegar dá um sinal binário que
/// serve para recomendar. O "amei" existe para separar "foi bom" de "quero
/// mais disso" — que é a informação que o recomendador realmente usa.
/// </remarks>
public class Avaliacao
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>O perfil que avaliou.</summary>
    public int PerfilId { get; set; }

    /// <summary>O perfil que avaliou.</summary>
    public Perfil? Perfil { get; set; }

    /// <summary>O título avaliado.</summary>
    public int TituloId { get; set; }

    /// <summary>O título avaliado.</summary>
    public Titulo? Titulo { get; set; }

    /// <summary>A nota.</summary>
    public Nota Nota { get; set; }

    /// <summary>Quando foi avaliado.</summary>
    public DateTimeOffset AvaliadoEm { get; set; }
}
