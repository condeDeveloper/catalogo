namespace Catalogo.Core.Modelo;

/// <summary>O que uma pessoa fez num título.</summary>
public enum Papel
{
    /// <summary>Atuou.</summary>
    Elenco = 1,

    /// <summary>Dirigiu.</summary>
    Direcao = 2,

    /// <summary>Escreveu.</summary>
    Roteiro = 3,
}

/// <summary>Alguém que participou de um título.</summary>
public class Pessoa
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>O nome.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>O nome sem acento, para a busca.</summary>
    public string NomeParaBusca { get; set; } = string.Empty;

    /// <summary>As participações.</summary>
    public List<Participacao> Participacoes { get; set; } = [];
}

/// <summary>
/// A participação de uma pessoa num título.
/// </summary>
/// <remarks>
/// A tabela do meio carrega dados próprios — o papel e o nome do personagem —
/// e por isso é uma entidade, não uma relação muitos-para-muitos simples. É a
/// diferença entre "esta pessoa está neste filme" e "esta pessoa faz tal
/// personagem neste filme".
/// </remarks>
public class Participacao
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>O título.</summary>
    public int TituloId { get; set; }

    /// <summary>O título.</summary>
    public Titulo? Titulo { get; set; }

    /// <summary>A pessoa.</summary>
    public int PessoaId { get; set; }

    /// <summary>A pessoa.</summary>
    public Pessoa? Pessoa { get; set; }

    /// <summary>O que ela fez.</summary>
    public Papel Papel { get; set; }

    /// <summary>O personagem, quando é elenco.</summary>
    public string? Personagem { get; set; }

    /// <summary>A ordem de crédito; menor aparece primeiro.</summary>
    public int Ordem { get; set; }
}
