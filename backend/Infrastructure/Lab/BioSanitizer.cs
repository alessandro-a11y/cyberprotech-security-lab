using System.Text.RegularExpressions;
using Infrastructure.Lab;

namespace Infrastructure.Lab;

/// <summary>
/// Sanitização opcional de texto livre (hoje, a bio).
/// </summary>
/// <remarks>
/// <para>
/// A API <b>não</b> é a parte vulnerável do cenário de XSS: a falha primária é
/// o frontend renderizar a bio com <c>dangerouslySetInnerHTML</c>. Armazenar e
/// devolver o texto como o usuário o digitou é o comportamento correto de uma
/// API — escaped é responsabilidade de quem renderiza.
/// </para>
/// <para>
/// Ainda assim, há uma segunda camada útil: quando o controle <c>vuln-mode</c>
/// está desligado, a API remove marcação da bio antes de gravar. Isso
/// <b>não</b> substitui escapar na renderização, mas reduz o raio de um XSS
/// armazenado e serve de contraponto didático: "filtro de entrada não é
/// proteção de saída".
/// </para>
/// <para>
/// Ligado, o texto passa intacto — que é o estado vulnerável, e o que permite
/// ao cenário do frontend ter payload de verdade para executar.
/// </para>
/// </remarks>
public sealed partial class BioSanitizer(LabState labState)
{
    /// <summary>
    /// Remove tags, entidades e o protocolo <c>javascript:</c>. Não é um
    /// sanitizador de HTML completo — de propósito, porque a proteção real está
    /// em escapar na saída.
    /// </summary>
    public string Sanitize(string? bio)
    {
        // No modo vulnerável o texto volta byte a byte: é o que dá munição ao
        // cenário de XSS do frontend, e aparar espaços quebraria a fidelidade
        // do payload.
        if (labState.Get(LabToggleId.VulnMode))
        {
            return bio ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(bio))
        {
            return string.Empty;
        }

        var cleaned = TagPattern().Replace(bio, string.Empty);
        cleaned = ScriptBlockPattern().Replace(cleaned, string.Empty);
        cleaned = EventHandlerPattern().Replace(cleaned, string.Empty);
        cleaned = JavascriptUriPattern().Replace(cleaned, string.Empty);
        cleaned = EntityPattern().Replace(cleaned, string.Empty);

        return cleaned.Trim();
    }

    [GeneratedRegex(@"<\s*/?\s*[a-zA-Z][^>]*>", RegexOptions.Compiled)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"<\s*script[^>]*>.*?<\s*/\s*script\s*>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlockPattern();

    [GeneratedRegex(@"\son[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerPattern();

    [GeneratedRegex(@"javascript\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptUriPattern();

    [GeneratedRegex(@"&(#[0-9]+|#x[0-9a-fA-F]+|[a-zA-Z]+);", RegexOptions.Compiled)]
    private static partial Regex EntityPattern();
}
