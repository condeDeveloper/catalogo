using Catalogo.Core.Modelo;

namespace Catalogo.Core.Perfis;

/// <summary>
/// A regra da fileira "continuar assistindo".
/// </summary>
/// <remarks>
/// <para>
/// É a regra de negócio mais interessante de um serviço de streaming, e a que
/// mais irrita quando está errada. Três decisões:
/// </para>
/// <list type="number">
///   <item>
///     <b>Começou de verdade?</b> Abrir e fechar em dez segundos não é
///     assistir. Sem um piso, a fileira enche de coisa que a pessoa só
///     espiou — e ela some no meio.
///   </item>
///   <item>
///     <b>Acabou?</b> Quem chega perto do fim viu os créditos. Sem um teto, o
///     filme terminado fica preso na fileira para sempre, e a pessoa precisa
///     removê-lo na mão.
///   </item>
///   <item>
///     <b>Série aponta para o próximo.</b> Terminar o episódio 3 não tira a
///     série da fileira: ela passa a apontar para o episódio 4, no segundo
///     zero. Só quando o último episódio termina é que a série sai.
///   </item>
/// </list>
/// </remarks>
public static class ContinuarAssistindo
{
    /// <summary>Abaixo disso, foi só uma espiada.</summary>
    public const double FracaoMinima = 0.02;

    /// <summary>A partir disso, considera-se assistido.</summary>
    public const double FracaoDeConclusao = 0.92;

    /// <summary>
    /// Nos primeiros minutos, a fração não serve.
    /// </summary>
    /// <remarks>
    /// Em episódio de 3 minutos, 2% são 4 segundos — tempo de errar o clique.
    /// Por isso vale o que for maior: a fração ou este piso em segundos.
    /// </remarks>
    public const int SegundosMinimos = 30;

    /// <summary>Um item pronto para a fileira.</summary>
    /// <param name="Titulo">O título a mostrar.</param>
    /// <param name="Episodio">O episódio a retomar, quando é série.</param>
    /// <param name="SegundoAtual">Onde retomar.</param>
    /// <param name="DuracaoEmSegundos">A duração da mídia a retomar.</param>
    /// <param name="AtualizadoEm">Quando foi visto pela última vez.</param>
    public record Item(
        Titulo Titulo,
        Episodio? Episodio,
        int SegundoAtual,
        int DuracaoEmSegundos,
        DateTimeOffset AtualizadoEm)
    {
        /// <summary>Quanto da barra de progresso preencher, de 0 a 1.</summary>
        public double Fracao => DuracaoEmSegundos <= 0 ? 0 : Math.Clamp((double)SegundoAtual / DuracaoEmSegundos, 0, 1);

        /// <summary>O rótulo da retomada.</summary>
        public string Rotulo => Episodio is null
            ? $"Faltam {Math.Max(1, (DuracaoEmSegundos - SegundoAtual) / 60)} min"
            : $"{Episodio.Codigo} · {Episodio.Nome}";
    }

    /// <summary>Se este progresso, sozinho, conta como "começado e não terminado".</summary>
    public static bool EmAndamento(Progresso progresso)
    {
        ArgumentNullException.ThrowIfNull(progresso);

        if (progresso.DuracaoEmSegundos <= 0)
        {
            return false;
        }

        var comecou = progresso.Fracao >= FracaoMinima || progresso.SegundoAtual >= SegundosMinimos;

        return comecou && progresso.Fracao < FracaoDeConclusao;
    }

    /// <summary>Se este progresso conta como assistido até o fim.</summary>
    public static bool Concluido(Progresso progresso)
    {
        ArgumentNullException.ThrowIfNull(progresso);

        return progresso.DuracaoEmSegundos > 0 && progresso.Fracao >= FracaoDeConclusao;
    }

    /// <summary>
    /// Monta a fileira a partir dos progressos de um perfil.
    /// </summary>
    /// <param name="progressos">Todos os progressos do perfil, com título e episódio carregados.</param>
    /// <param name="limite">Quantos itens no máximo.</param>
    public static IReadOnlyList<Item> Montar(IEnumerable<Progresso> progressos, int limite = 20)
    {
        ArgumentNullException.ThrowIfNull(progressos);

        var porTitulo = progressos
            .Where(p => p.Titulo is not null && !p.Titulo.Removido)
            .GroupBy(p => p.TituloId);

        var itens = new List<Item>();

        foreach (var grupo in porTitulo)
        {
            var item = DoTitulo(grupo.ToList());

            if (item is not null)
            {
                itens.Add(item);
            }
        }

        return itens
            .OrderByDescending(i => i.AtualizadoEm)
            .Take(limite)
            .ToList();
    }

    /// <summary>Decide o que mostrar para um título, dados os progressos dele.</summary>
    private static Item? DoTitulo(List<Progresso> progressos)
    {
        var maisRecente = progressos.OrderByDescending(p => p.AtualizadoEm).First();
        var titulo = maisRecente.Titulo!;

        if (titulo.Tipo == TipoDeTitulo.Filme)
        {
            return EmAndamento(maisRecente)
                ? new Item(titulo, null, maisRecente.SegundoAtual, maisRecente.DuracaoEmSegundos, maisRecente.AtualizadoEm)
                : null;
        }

        // Série: se o episódio mais recente ainda está no meio, é ele.
        if (EmAndamento(maisRecente) && maisRecente.Episodio is not null)
        {
            return new Item(
                titulo,
                maisRecente.Episodio,
                maisRecente.SegundoAtual,
                maisRecente.DuracaoEmSegundos,
                maisRecente.AtualizadoEm);
        }

        if (!Concluido(maisRecente) || maisRecente.Episodio is null)
        {
            return null;
        }

        // O episódio acabou: a série continua na fileira, apontando para o
        // próximo — no segundo zero. É o que faz "continuar assistindo"
        // significar a série, e não um episódio parado.
        var proximo = ProximoEpisodio(titulo, maisRecente.Episodio);

        if (proximo is null)
        {
            return null;
        }

        return new Item(titulo, proximo, 0, proximo.DuracaoEmMinutos * 60, maisRecente.AtualizadoEm);
    }

    /// <summary>
    /// O episódio seguinte, atravessando a virada de temporada.
    /// </summary>
    /// <remarks>
    /// O último episódio de uma temporada leva ao primeiro da seguinte, e o
    /// último da série não leva a lugar nenhum. Esquecer a virada faz a série
    /// sumir da fileira no fim de cada temporada — exatamente quando a pessoa
    /// mais provavelmente vai continuar.
    /// </remarks>
    public static Episodio? ProximoEpisodio(Titulo serie, Episodio atual)
    {
        ArgumentNullException.ThrowIfNull(serie);
        ArgumentNullException.ThrowIfNull(atual);

        var emOrdem = serie.Temporadas
            .OrderBy(t => t.Numero)
            .SelectMany(t => t.Episodios.OrderBy(e => e.Numero).Select(e => (Temporada: t, Episodio: e)))
            .ToList();

        var posicao = emOrdem.FindIndex(p => p.Episodio.Id == atual.Id);

        if (posicao < 0 || posicao + 1 >= emOrdem.Count)
        {
            return null;
        }

        var seguinte = emOrdem[posicao + 1];

        // A temporada precisa vir junto: o código "T2:E1" depende dela.
        seguinte.Episodio.Temporada ??= seguinte.Temporada;

        return seguinte.Episodio;
    }
}
