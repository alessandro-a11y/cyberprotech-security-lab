using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CyberProtech.Tests.Integration;

/// <summary>
/// Autenticação, papéis e controle de acesso, contra a API real.
/// </summary>
public class AutenticacaoTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory factory;
    private string tokenAdmin = string.Empty;
    private string tokenAluno = string.Empty;
    private string estadoDoLaboratorio = string.Empty;

    public AutenticacaoTests(ApiFactory factory) => this.factory = factory;

    public async Task InitializeAsync()
    {
        // Sem banco, o token não sai. O Skip.IfNot de cada teste é que avisa.
        if (!ApiFactory.DatabaseDisponivel())
        {
            return;
        }

        tokenAdmin = await factory.ObterTokenAsync(ApiFactory.AdminUsername) ?? string.Empty;
        tokenAluno = await factory.ObterTokenAsync("aluno01") ?? string.Empty;

        if (string.IsNullOrEmpty(tokenAdmin))
        {
            estadoDoLaboratorio = await DescreverLaboratorioAsync();
        }
    }

    private async Task<string> DescreverLaboratorioAsync()
    {
        try
        {
            using var client = factory.CriarClienteAutenticado(await ObterTokenProvisorioAsync());
            using var resposta = await client.GetAsync("/api/lab/config");
            return await resposta.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"falha ao ler: {ex.Message}";
        }
    }

    private async Task<string> ObterTokenProvisorioAsync()
    {
        var login = await factory.ObterTokenAsync(ApiFactory.AdminUsername);
        return login ?? string.Empty;
    }

    public async Task DisposeAsync()
    {
        if (ApiFactory.DatabaseDisponivel() && !string.IsNullOrEmpty(tokenAdmin))
        {
            await factory.RestaurarEstadoCorrigidoAsync();
        }
    }

    [SkippableFact]
    public async Task Login_devolve_token_expiracao_e_usuario()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        using var resposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = ApiFactory.TestPassword,
        });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(corpo.GetProperty("token").GetString()));
        Assert.True(corpo.GetProperty("expiresAt").GetDateTime() > DateTime.UtcNow);
        Assert.Equal("Admin", corpo.GetProperty("user").GetProperty("role").GetString());
    }

    [SkippableTheory]
    [InlineData("admin", "senha-errada")]
    [InlineData("nao-existe", "senha-errada")]
    public async Task Credenciais_erradas_devolvem_401(string usuario, string senha)
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        using var resposta = await client.PostAsJsonAsync("/api/auth/login", new { username = usuario, password = senha });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Senha_vazia_e_400_por_validacao_e_nao_401()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        using var resposta = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "" });

        // A [Required] do LoginRequest barra antes do controller. 400 aqui é o
        // comportamento certo: nem chega a gastar um BCrypt.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Usuario_inexistente_e_senha_errada_dao_a_mesma_mensagem()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();

        using var comSenhaErrada = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "senha-que-passa-na-validacao" });
        Assert.Equal(HttpStatusCode.Unauthorized, comSenhaErrada.StatusCode);
        var corpo1 = await comSenhaErrada.Content.ReadFromJsonAsync<JsonElement>();

        using var comUsuarioInexistente = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "nao-existe-mesmo", password = "senha-que-passa-na-validacao" });
        Assert.Equal(HttpStatusCode.Unauthorized, comUsuarioInexistente.StatusCode);
        var corpo2 = await comUsuarioInexistente.Content.ReadFromJsonAsync<JsonElement>();

        // Enumeração de usuário: a mensagem precisa ser indistinguível. O
        // traceId é propositalmente único por requisição, então comparar o
        // corpo inteiro testaria o contrário do que queremos.
        Assert.Equal(corpo1.GetProperty("title").GetString(), corpo2.GetProperty("title").GetString());
        Assert.Equal(corpo1.GetProperty("status").GetInt32(), corpo2.GetProperty("status").GetInt32());
        Assert.NotEqual(corpo1.GetProperty("traceId").GetString(), corpo2.GetProperty("traceId").GetString());
    }

    [SkippableFact]
    public async Task Sem_token_a_lista_de_usuarios_e_401()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        using var resposta = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Token_invalido_e_401()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "nao.e.um.token");

        using var resposta = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Auth_me_devolve_o_usuario_do_token()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), $"Login não disponível. Lab: {estadoDoLaboratorio}");

        using var client = factory.CriarClienteAutenticado(tokenAdmin);
        using var resposta = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin", corpo.GetProperty("username").GetString());
    }

    [SkippableFact]
    public async Task Nenhum_hash_de_senha_vaza_na_resposta()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), $"Login não disponível. Lab: {estadoDoLaboratorio}");

        using var client = factory.CriarClienteAutenticado(tokenAdmin);
        var json = await client.GetStringAsync("/api/users?pageSize=100");

        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2a$", json, StringComparison.Ordinal);
        Assert.DoesNotContain("$2b$", json, StringComparison.Ordinal);
        Assert.DoesNotContain("!pending", json, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Cadastro_cria_usuario_com_papel_User_mesmo_que_o_corpo_peca_Admin()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        using var resposta = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"novo{sufixo}",
            email = $"novo{sufixo}@cyberprotech.lab",
            password = "SenhaForte@123",
            role = "Admin", // tentativa de escalada
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("User", corpo.GetProperty("user").GetProperty("role").GetString());
    }

    [SkippableTheory]
    [InlineData("curto", "curto@lab.lab", "123")]           // senha curta
    [InlineData("a", "curto@lab.lab", "SenhaForte@123")]   // usuário curto
    [InlineData("okuser", "nao-e-email", "SenhaForte@123")] // e-mail inválido
    public async Task Cadastro_invalido_e_400(string usuario, string email, string senha)
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        using var resposta = await client.PostAsJsonAsync("/api/auth/register",
            new { username = usuario, email = email, password = senha });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Cadastro_com_usuario_ja_existente_e_409()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        using var resposta = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "admin",
            email = "novo-email@cyberprotech.lab",
            password = "SenhaForte@123",
        });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Troca_de_senha_exige_a_senha_atual()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var usuario = $"troca{sufixo}";

        using var client = factory.CreateClient();
        using var cadastro = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = usuario,
            email = $"{usuario}@cyberprotech.lab",
            password = "SenhaAntiga@123",
        });

        Assert.Equal(HttpStatusCode.Created, cadastro.StatusCode);
        var token = (await cadastro.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString()!;

        var autenticado = factory.CriarClienteAutenticado(token);

        using var comSenhaErrada = await autenticado.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = "errada",
            newPassword = "SenhaNova@123",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, comSenhaErrada.StatusCode);

        using var comSenhaCerta = await autenticado.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = "SenhaAntiga@123",
            newPassword = "SenhaNova@123",
        });

        Assert.Equal(HttpStatusCode.NoContent, comSenhaCerta.StatusCode);

        // A senha nova funciona e a antiga deixou de funcionar.
        using var loginNovo = await client.PostAsJsonAsync("/api/auth/login",
            new { username = usuario, password = "SenhaNova@123" });
        Assert.Equal(HttpStatusCode.OK, loginNovo.StatusCode);

        using var loginAntigo = await client.PostAsJsonAsync("/api/auth/login",
            new { username = usuario, password = "SenhaAntiga@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginAntigo.StatusCode);
    }
}
