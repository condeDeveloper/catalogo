using Catalogo.Core.Consulta;
using Catalogo.Core.Modelo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Catalogo.Core.Armazenamento;

/// <summary>O banco do catálogo.</summary>
public class ContextoDoCatalogo(DbContextOptions<ContextoDoCatalogo> opcoes) : DbContext(opcoes)
{
    /// <summary>Os títulos.</summary>
    public DbSet<Titulo> Titulos => Set<Titulo>();

    /// <summary>As temporadas.</summary>
    public DbSet<Temporada> Temporadas => Set<Temporada>();

    /// <summary>Os episódios.</summary>
    public DbSet<Episodio> Episodios => Set<Episodio>();

    /// <summary>Os gêneros.</summary>
    public DbSet<Genero> Generos => Set<Genero>();

    /// <summary>As pessoas.</summary>
    public DbSet<Pessoa> Pessoas => Set<Pessoa>();

    /// <summary>As participações.</summary>
    public DbSet<Participacao> Participacoes => Set<Participacao>();

    /// <summary>Os perfis.</summary>
    public DbSet<Perfil> Perfis => Set<Perfil>();

    /// <summary>Os itens de lista.</summary>
    public DbSet<ItemDaLista> ItensDaLista => Set<ItemDaLista>();

    /// <summary>Os progressos.</summary>
    public DbSet<Progresso> Progressos => Set<Progresso>();

    /// <summary>As avaliações.</summary>
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();

    /// <summary>
    /// Guarda todo <see cref="DateTimeOffset"/> como binário.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O SQLite não tem tipo de data: ele guarda texto. E o provedor recusa,
    /// com <c>NotSupportedException</c>, qualquer <c>ORDER BY</c> ou
    /// comparação sobre <see cref="DateTimeOffset"/> — porque comparar o
    /// texto "2026-09-24T12:00:00-03:00" com "2026-09-24T11:00:00-06:00"
    /// daria a ordem errada: o segundo é depois, e alfabeticamente vem antes.
    /// </para>
    /// <para>
    /// O conversor binário resolve os dois de uma vez: grava um número que
    /// ordena por instante absoluto e ainda preserva o fuso original. Sem ele,
    /// a listagem do catálogo — que ordena por data de entrada — simplesmente
    /// não roda.
    /// </para>
    /// </remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder construtor)
    {
        ArgumentNullException.ThrowIfNull(construtor);

        construtor.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder construtor)
    {
        ArgumentNullException.ThrowIfNull(construtor);

        construtor.Entity<Titulo>(titulo =>
        {
            titulo.Property(t => t.Nome).IsRequired().HasMaxLength(200);
            titulo.Property(t => t.NomeParaBusca).IsRequired().HasMaxLength(200);
            titulo.Property(t => t.Sinopse).HasMaxLength(2000);
            titulo.Property(t => t.CapaUrl).HasMaxLength(500);

            // O índice da busca é sobre a coluna já normalizada — é ele que
            // faz a diferença entre buscar em 1 ms e varrer a tabela.
            titulo.HasIndex(t => t.NomeParaBusca);

            // A listagem padrão ordena por estes dois, nesta ordem. Sem o
            // índice composto, toda rolagem de tela vira ordenação em memória.
            titulo.HasIndex(t => new { t.Removido, t.AdicionadoEm });

            titulo.HasMany(t => t.Generos)
                .WithMany(g => g.Titulos)
                .UsingEntity(juncao => juncao.ToTable("TituloGenero"));

            titulo.HasMany(t => t.Temporadas)
                .WithOne(t => t.Titulo!)
                .HasForeignKey(t => t.TituloId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        construtor.Entity<Temporada>(temporada =>
        {
            temporada.Property(t => t.Nome).HasMaxLength(200);

            // Duas temporadas 1 na mesma série é dado corrompido, não um caso
            // de uso; o banco é o único lugar que garante isso sob concorrência.
            temporada.HasIndex(t => new { t.TituloId, t.Numero }).IsUnique();

            temporada.HasMany(t => t.Episodios)
                .WithOne(e => e.Temporada!)
                .HasForeignKey(e => e.TemporadaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        construtor.Entity<Episodio>(episodio =>
        {
            episodio.Property(e => e.Nome).IsRequired().HasMaxLength(200);
            episodio.Property(e => e.MidiaUrl).IsRequired().HasMaxLength(500);
            episodio.HasIndex(e => new { e.TemporadaId, e.Numero }).IsUnique();
        });

        construtor.Entity<Genero>(genero =>
        {
            genero.Property(g => g.Nome).IsRequired().HasMaxLength(60);
            genero.Property(g => g.Apelido).IsRequired().HasMaxLength(60);
            genero.HasIndex(g => g.Apelido).IsUnique();
        });

        construtor.Entity<Pessoa>(pessoa =>
        {
            pessoa.Property(p => p.Nome).IsRequired().HasMaxLength(160);
            pessoa.Property(p => p.NomeParaBusca).IsRequired().HasMaxLength(160);
            pessoa.HasIndex(p => p.NomeParaBusca);
        });

        construtor.Entity<Participacao>(participacao =>
        {
            participacao.Property(p => p.Personagem).HasMaxLength(160);

            participacao.HasOne(p => p.Titulo!)
                .WithMany(t => t.Participacoes)
                .HasForeignKey(p => p.TituloId)
                .OnDelete(DeleteBehavior.Cascade);

            participacao.HasOne(p => p.Pessoa!)
                .WithMany(p => p.Participacoes)
                .HasForeignKey(p => p.PessoaId)
                .OnDelete(DeleteBehavior.Cascade);

            participacao.HasIndex(p => new { p.TituloId, p.PessoaId, p.Papel }).IsUnique();
        });

        construtor.Entity<Perfil>(perfil =>
        {
            perfil.Property(p => p.Nome).IsRequired().HasMaxLength(60);
            perfil.Property(p => p.Cor).IsRequired().HasMaxLength(9);
        });

        construtor.Entity<ItemDaLista>(item =>
        {
            // O mesmo título duas vezes na mesma lista não é erro do usuário —
            // é clique duplo. O índice único transforma isso em nada.
            item.HasIndex(i => new { i.PerfilId, i.TituloId }).IsUnique();

            item.HasOne(i => i.Perfil!).WithMany(p => p.Lista).HasForeignKey(i => i.PerfilId);
            item.HasOne(i => i.Titulo!).WithMany().HasForeignKey(i => i.TituloId);
        });

        construtor.Entity<Progresso>(progresso =>
        {
            // Um progresso por (perfil, título, episódio). Para filme o
            // episódio é nulo, e no SQLite vários nulos não colidem num índice
            // único — que é justamente o comportamento desejado aqui.
            progresso.HasIndex(p => new { p.PerfilId, p.TituloId, p.EpisodioId }).IsUnique();

            progresso.HasIndex(p => new { p.PerfilId, p.AtualizadoEm });

            progresso.HasOne(p => p.Perfil!).WithMany(p => p.Progressos).HasForeignKey(p => p.PerfilId);
            progresso.HasOne(p => p.Titulo!).WithMany().HasForeignKey(p => p.TituloId);
            progresso.HasOne(p => p.Episodio!).WithMany().HasForeignKey(p => p.EpisodioId);
        });

        construtor.Entity<Avaliacao>(avaliacao =>
        {
            avaliacao.HasIndex(a => new { a.PerfilId, a.TituloId }).IsUnique();

            avaliacao.HasOne(a => a.Perfil!).WithMany(p => p.Avaliacoes).HasForeignKey(a => a.PerfilId);
            avaliacao.HasOne(a => a.Titulo!).WithMany().HasForeignKey(a => a.TituloId);
        });
    }

    /// <summary>
    /// Mantém as colunas de busca em dia antes de gravar.
    /// </summary>
    /// <remarks>
    /// Fazer isso aqui, e não em cada endpoint, é o que garante que nenhuma
    /// gravação escape — inclusive as que ainda nem foram escritas. Um nome
    /// atualizado sem a coluna de busca junto some da busca em silêncio.
    /// </remarks>
    public override int SaveChanges()
    {
        NormalizarNomes();

        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool aceitarTudo, CancellationToken cancelamento = default)
    {
        NormalizarNomes();

        return base.SaveChangesAsync(aceitarTudo, cancelamento);
    }

    private void NormalizarNomes()
    {
        foreach (var entrada in ChangeTracker.Entries<Titulo>())
        {
            if (entrada.State is EntityState.Added or EntityState.Modified)
            {
                entrada.Entity.NomeParaBusca = Normalizacao.Normalizar(entrada.Entity.Nome);
            }
        }

        foreach (var entrada in ChangeTracker.Entries<Pessoa>())
        {
            if (entrada.State is EntityState.Added or EntityState.Modified)
            {
                entrada.Entity.NomeParaBusca = Normalizacao.Normalizar(entrada.Entity.Nome);
            }
        }
    }
}
