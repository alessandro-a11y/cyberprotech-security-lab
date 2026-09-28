namespace CyberProtech.Load;

/// <summary>
/// Opções de linha de comando do gerador de carga.
/// </summary>
/// <remarks>
/// Nomes com traço duplo para não colidirem com o prefixo de switch do .NET.
/// A senha tem o mesmo cuidado: passar senha na linha de comando é aceito aqui
/// porque é ferramenta de laboratório e o valor é público no repositório, mas o
/// valor padrão não é segredo nenhum.
/// </remarks>
internal sealed class Opcoes
{
    public string BaseUrl { get; private init; } = "http://localhost:5000";
    public string? Cenario { get; private init; }
    public int Concorrencia { get; private init; } = 10;
    public int DuracaoSegundos { get; private init; } = 20;
    public int RampaSegundos { get; private init; } = 2;
    public int AquecimentoSegundos { get; private init; } = 3;
    public string Usuario { get; private init; } = "admin";
    public string Senha { get; private init; } = "CyberProtech@2026";
    public string TermoBusca { get; private init; } = "admin";
    public string IdAlvo { get; private init; } = "3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e01";
    public bool Ajuda { get; private init; }

    public bool NeedsToken() => Cenario is not ("health" or "ready" or "login");

    public static Opcoes Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new Opcoes { Ajuda = true };
        }

        string? Pegar(string chave)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == chave)
                {
                    return i + 1 < args.Length ? args[i + 1] : null;
                }
            }

            return null;
        }

        return new Opcoes
        {
            Ajuda = args.Contains("--ajuda") || args.Contains("-h"),
            BaseUrl = Pegar("--url") ?? "http://localhost:5000",
            Cenario = Pegar("--cenario"),
            Concorrencia = int.TryParse(Pegar("--concorrencia"), out var c) ? c : 10,
            DuracaoSegundos = int.TryParse(Pegar("--duracao"), out var d) ? d : 20,
            RampaSegundos = int.TryParse(Pegar("--rampa"), out var r) ? r : 2,
            AquecimentoSegundos = int.TryParse(Pegar("--aquecimento"), out var a) ? a : 3,
            Usuario = Pegar("--usuario") ?? "admin",
            Senha = Pegar("--senha") ?? "CyberProtech@2026",
            TermoBusca = Pegar("--busca") ?? "admin",
            IdAlvo = Pegar("--id") ?? "3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e01",
        };
    }

    public static void ImprimirAjuda()
    {
        Console.WriteLine("CyberProtech.Load — gerador de carga da API");
        Console.WriteLine();
        Console.WriteLine("uso:");
        Console.WriteLine("  dotnet run --project tests/load -- --cenario <nome> [opções]");
        Console.WriteLine();
        Console.WriteLine("cenários:");
        foreach (var cenario in Cenarios.Todos)
        {
            Console.WriteLine($"  {cenario}");
        }

        Console.WriteLine();
        Console.WriteLine("opções:");
        Console.WriteLine("  --url           endereço da API (padrão http://localhost:5000)");
        Console.WriteLine("  --cenario       um dos nomes acima");
        Console.WriteLine("  --concorrencia  workers simultâneos (padrão 10)");
        Console.WriteLine("  --duracao       segundos de carga (padrão 20)");
        Console.WriteLine("  --rampa         segundos para subir a carga (padrão 2)");
        Console.WriteLine("  --aquecimento   segundos descartados antes de medir (padrão 3)");
        Console.WriteLine("  --usuario       usuário do login (padrão admin)");
        Console.WriteLine("  --senha         senha do login (padrão CyberProtech@2026)");
        Console.WriteLine("  --busca         termo do cenário users-busca (padrão admin)");
        Console.WriteLine("  --id            id do cenário user-por-id");
        Console.WriteLine("  --ajuda         esta ajuda");
        Console.WriteLine();
        Console.WriteLine("exemplo:");
        Console.WriteLine("  dotnet run --project tests/load -- --cenario users --concorrencia 50 --duracao 30");
    }
}
