namespace Catalogo.Core.Modelo;

/// <summary>Filme ou série.</summary>
public enum TipoDeTitulo
{
    /// <summary>Uma obra fechada, com uma mídia só.</summary>
    Filme = 1,

    /// <summary>Uma obra em temporadas e episódios.</summary>
    Serie = 2,
}

/// <summary>
/// Um título do catálogo.
/// </summary>
/// <remarks>
/// <para>
/// Filme e série moram na mesma tabela de propósito. A alternativa — duas
/// tabelas — parece mais limpa e cobra caro na hora de montar a tela inicial,
/// que precisa misturar os dois em cada fileira, ordenados juntos e paginados
/// juntos. Com duas tabelas isso vira união de consultas com paginação
/// manual, e a busca precisa ser feita duas vezes.
/// </para>
/// <para>
/// A diferença entre os dois fica em dois campos que se excluem: filme tem
/// <see cref="DuracaoEmMinutos"/> e <see cref="MidiaUrl"/>; série tem
/// <see cref="Temporadas"/>. <see cref="Validar"/> faz essa regra valer.
/// </para>
/// </remarks>
public class Titulo
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>Filme ou série.</summary>
    public TipoDeTitulo Tipo { get; set; }

    /// <summary>O nome como aparece na tela.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// O nome sem acento e em minúsculas, para a busca.
    /// </summary>
    /// <remarks>
    /// Guardado em coluna própria porque o SQLite não sabe tirar acento por
    /// conta: <c>LIKE '%cao%'</c> não acha "coração". Normalizar na gravação
    /// custa uma vez; normalizar na consulta custaria em toda busca e ainda
    /// impediria o uso de índice.
    /// </remarks>
    public string NomeParaBusca { get; set; } = string.Empty;

    /// <summary>A sinopse.</summary>
    public string Sinopse { get; set; } = string.Empty;

    /// <summary>O ano de lançamento.</summary>
    public int Ano { get; set; }

    /// <summary>A classificação indicativa.</summary>
    public ClassificacaoIndicativa Classificacao { get; set; }

    /// <summary>A duração, em minutos. Só para filme.</summary>
    public int? DuracaoEmMinutos { get; set; }

    /// <summary>A playlist HLS do filme. Só para filme.</summary>
    public string? MidiaUrl { get; set; }

    /// <summary>
    /// A arte vertical, quando existe uma.
    /// </summary>
    /// <remarks>
    /// Opcional de propósito. Sem arte, a tela desenha a capa a partir do nome
    /// e de uma cor derivada do id — o que mantém o catálogo utilizável sem
    /// depender de imagem de terceiro e sem um arquivo sequer no repositório.
    /// </remarks>
    public string? CapaUrl { get; set; }

    /// <summary>A arte horizontal, usada no destaque da tela inicial.</summary>
    public string? BannerUrl { get; set; }

    /// <summary>A quem creditar a obra, quando ela é de licença aberta.</summary>
    public string? Credito { get; set; }

    /// <summary>Se aparece no destaque da tela inicial.</summary>
    public bool Destaque { get; set; }

    /// <summary>Quando entrou no catálogo. Ordena a fileira de lançamentos.</summary>
    public DateTimeOffset AdicionadoEm { get; set; }

    /// <summary>
    /// Se o título saiu do catálogo.
    /// </summary>
    /// <remarks>
    /// Remoção é lógica, não física. Apagar de verdade levaria junto o
    /// progresso de quem estava assistindo e as avaliações — e um título que
    /// sai do ar hoje pode voltar no mês que vem, com o histórico intacto.
    /// </remarks>
    public bool Removido { get; set; }

    /// <summary>Os gêneros.</summary>
    public List<Genero> Generos { get; set; } = [];

    /// <summary>Quem atuou e quem dirigiu.</summary>
    public List<Participacao> Participacoes { get; set; } = [];

    /// <summary>As temporadas. Só para série.</summary>
    public List<Temporada> Temporadas { get; set; } = [];

    /// <summary>Quantos episódios a série tem ao todo.</summary>
    public int TotalDeEpisodios => Temporadas.Sum(t => t.Episodios.Count);

    /// <summary>
    /// Confere as regras que o tipo impõe.
    /// </summary>
    /// <exception cref="ArgumentException">Quando o título está incoerente.</exception>
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Nome))
        {
            throw new ArgumentException("O título precisa de nome.");
        }

        if (Ano < 1888 || Ano > DateTimeOffset.UtcNow.Year + 5)
        {
            // 1888 é o ano de "Roundhay Garden Scene", o filme mais antigo que
            // sobreviveu. Abaixo disso é erro de digitação, não acervo.
            throw new ArgumentException($"Ano fora do razoável: {Ano}.");
        }

        if (Tipo == TipoDeTitulo.Filme)
        {
            if (DuracaoEmMinutos is null or <= 0)
            {
                throw new ArgumentException("Filme precisa de duração em minutos.");
            }

            if (string.IsNullOrWhiteSpace(MidiaUrl))
            {
                throw new ArgumentException("Filme precisa da URL da mídia.");
            }

            if (Temporadas.Count > 0)
            {
                throw new ArgumentException("Filme não tem temporada.");
            }

            return;
        }

        if (DuracaoEmMinutos is not null || MidiaUrl is not null)
        {
            throw new ArgumentException("Série não tem duração nem mídia própria; quem tem são os episódios.");
        }
    }
}
