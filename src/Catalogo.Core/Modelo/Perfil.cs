namespace Catalogo.Core.Modelo;

/// <summary>
/// Um perfil da conta.
/// </summary>
/// <remarks>
/// O perfil, e não a conta, é a unidade de tudo que é pessoal: lista,
/// progresso e avaliação. É o que faz o "continuar assistindo" de quem divide
/// a assinatura não se misturar — e é por isso que a chave estrangeira desses
/// três aponta para cá.
/// </remarks>
public class Perfil
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>O nome que aparece na seleção de perfil.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>A cor do avatar, em hexadecimal.</summary>
    public string Cor { get; set; } = "#A52A45";

    /// <summary>
    /// Se é um perfil infantil.
    /// </summary>
    /// <remarks>
    /// Guardar só o booleano não basta: "infantil" precisa virar uma idade
    /// para comparar com a classificação. Quem faz isso é a
    /// <see cref="IdadeMaxima"/>, e é ela que o filtro usa.
    /// </remarks>
    public bool Infantil { get; set; }

    /// <summary>
    /// O limite de idade deste perfil, quando há um.
    /// </summary>
    /// <remarks>
    /// Um perfil adulto sem limite fica <c>null</c> e vê tudo. Um perfil
    /// infantil sem limite explícito cai em 10 anos, que é a faixa que a
    /// maioria dos serviços usa como padrão do modo infantil.
    /// </remarks>
    public int? LimiteDeIdade { get; set; }

    /// <summary>A idade máxima que este perfil pode ver.</summary>
    public int IdadeMaxima => LimiteDeIdade ?? (Infantil ? 10 : 18);

    /// <summary>Quando o perfil foi criado.</summary>
    public DateTimeOffset CriadoEm { get; set; }

    /// <summary>Os itens da lista.</summary>
    public List<ItemDaLista> Lista { get; set; } = [];

    /// <summary>O progresso de reprodução.</summary>
    public List<Progresso> Progressos { get; set; } = [];

    /// <summary>As avaliações.</summary>
    public List<Avaliacao> Avaliacoes { get; set; } = [];
}
