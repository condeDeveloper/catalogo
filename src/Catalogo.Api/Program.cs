using Catalogo.Api.Endpoints;
using Catalogo.Core.Armazenamento;
using Microsoft.EntityFrameworkCore;

var construtor = WebApplication.CreateBuilder(args);

// O banco fica num arquivo do lado do executável por padrão. Em teste, o
// `WebApplicationFactory` troca isto por SQLite em memória — por isso a
// escolha vem da configuração, e não está fixa no código.
var conexao = construtor.Configuration.GetConnectionString("Catalogo") ?? "Data Source=catalogo.db";

construtor.Services.AddDbContext<ContextoDoCatalogo>(opcoes => opcoes.UseSqlite(conexao));

construtor.Services.AddEndpointsApiExplorer();
construtor.Services.AddSwaggerGen(opcoes => opcoes.SwaggerDoc("v1", new() { Title = "Catálogo", Version = "v1" }));

// A tela roda no GitHub Pages, em outra origem. Sem isto, o navegador recusa
// toda resposta — e o erro aparece no console do navegador, não no log da API,
// que é o que torna esse problema tão demorado de diagnosticar.
construtor.Services.AddCors(opcoes => opcoes.AddDefaultPolicy(politica => politica
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = construtor.Build();

using (var escopo = app.Services.CreateScope())
{
    var contexto = escopo.ServiceProvider.GetRequiredService<ContextoDoCatalogo>();

    contexto.Database.EnsureCreated();
    Semente.Semear(contexto, DateTimeOffset.UtcNow);
}

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new
{
    nome = "Catálogo",
    versao = "1.0",
    documentacao = "/swagger",
}));

app.MapGet("/saude", async (ContextoDoCatalogo contexto) => Results.Ok(new
{
    ok = true,
    titulos = await contexto.Titulos.CountAsync(t => !t.Removido),
}));

app.MapCatalogo();
app.MapTitulos();
app.MapPerfis();
app.MapInicio();

await app.RunAsync();

/// <summary>Exposta para o <c>WebApplicationFactory</c> dos testes enxergar.</summary>
public partial class Program
{
    /// <summary>Construtor vazio para o gerador de origem não reclamar.</summary>
    protected Program()
    {
    }
}
