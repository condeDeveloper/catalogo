namespace Catalogo.Core.Modelo;

/// <summary>Um título que o perfil marcou para ver depois.</summary>
public class ItemDaLista
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>O perfil dono da lista.</summary>
    public int PerfilId { get; set; }

    /// <summary>O perfil dono da lista.</summary>
    public Perfil? Perfil { get; set; }

    /// <summary>O título marcado.</summary>
    public int TituloId { get; set; }

    /// <summary>O título marcado.</summary>
    public Titulo? Titulo { get; set; }

    /// <summary>
    /// Quando foi marcado.
    /// </summary>
    /// <remarks>
    /// A lista sai do mais recente para o mais antigo, e não em ordem
    /// alfabética: quem acabou de marcar quer ver aquilo primeiro.
    /// </remarks>
    public DateTimeOffset AdicionadoEm { get; set; }
}
