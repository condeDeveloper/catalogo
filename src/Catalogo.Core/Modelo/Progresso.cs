namespace Catalogo.Core.Modelo;

/// <summary>
/// Onde um perfil parou num título.
/// </summary>
/// <remarks>
/// <para>
/// A linha aponta para o <see cref="TituloId"/> e, quando é série, também
/// para o <see cref="EpisodioId"/>. Guardar só o episódio pareceria mais
/// limpo e cobraria caro: a fileira "continuar assistindo" mostra a
/// <b>série</b>, não o episódio solto, e descobrir a série a partir do
/// episódio exigiria dois saltos em toda linha.
/// </para>
/// <para>
/// Uma série tem uma linha por episódio começado, e a fileira escolhe qual
/// mostrar — ver <c>ContinuarAssistindo</c>.
/// </para>
/// </remarks>
public class Progresso
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>O perfil.</summary>
    public int PerfilId { get; set; }

    /// <summary>O perfil.</summary>
    public Perfil? Perfil { get; set; }

    /// <summary>O título.</summary>
    public int TituloId { get; set; }

    /// <summary>O título.</summary>
    public Titulo? Titulo { get; set; }

    /// <summary>O episódio, quando é série.</summary>
    public int? EpisodioId { get; set; }

    /// <summary>O episódio, quando é série.</summary>
    public Episodio? Episodio { get; set; }

    /// <summary>Em que segundo parou.</summary>
    public int SegundoAtual { get; set; }

    /// <summary>
    /// A duração da mídia, em segundos.
    /// </summary>
    /// <remarks>
    /// Copiada para cá de propósito, em vez de ser buscada do título. A
    /// duração pode mudar quando a mídia é reenviada, e o percentual de quem
    /// assistiu antes passaria a ser calculado contra outro número — fazendo
    /// um filme concluído voltar para a fileira de "continuar assistindo".
    /// </remarks>
    public int DuracaoEmSegundos { get; set; }

    /// <summary>Quando foi atualizado pela última vez.</summary>
    public DateTimeOffset AtualizadoEm { get; set; }

    /// <summary>Quanto do conteúdo já passou, de 0 a 1.</summary>
    public double Fracao => DuracaoEmSegundos <= 0 ? 0 : Math.Clamp((double)SegundoAtual / DuracaoEmSegundos, 0, 1);

    /// <summary>Quanto falta, em segundos.</summary>
    public int SegundosRestantes => Math.Max(0, DuracaoEmSegundos - SegundoAtual);
}
