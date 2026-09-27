namespace Application.DTOs;

/// <summary>
/// Estado atual dos controles do laboratório.
/// </summary>
/// <remarks>
/// Os <c>id</c> são os mesmos que a tela de Administração já usa
/// (<c>frontend/src/pages/Admin.jsx</c>), para o frontend não precisar de
/// translating.
/// </remarks>
/// <param name="LabEnabled">
/// Se o laboratório pode ser alternado. Vem de <c>Lab:Enabled</c> e do ambiente;
/// quando falso, <c>Writable</c> também é falso e os controles ficam inertes.
/// </param>
/// <param name="Writable">
/// Se este usuário pode alterar os controles. Só Admin, e só com o laboratório
/// habilitado.
/// </param>
/// <param name="Toggles">Controles e seus estados.</param>
public sealed record LabConfigResponse(
    bool LabEnabled,
    bool Writable,
    IReadOnlyList<LabToggle> Toggles);

/// <summary>
/// Um controle do laboratório.
/// </summary>
/// <param name="Id">Identificador estável, usado pelo frontend.</param>
/// <param name="Label">Rótulo exibido na interface.</param>
/// <param name="Hint">Descrição curta do efeito.</param>
/// <param name="On">Estado atual.</param>
public sealed record LabToggle(string Id, string Label, string Hint, bool On);

/// <summary>
/// Alteração de um ou mais controles.
/// </summary>
/// <remarks>
/// Só os campos enviados mudam; os ausentes ficam como estão. É assim que o
/// toggle do frontend envia um controle por vez.
/// </remarks>
public sealed class LabConfigUpdateRequest
{
    public bool? VulnMode { get; set; }

    public bool? VerboseErrors { get; set; }

    public bool? RateLimit { get; set; }

    public bool? SecurityHeaders { get; set; }

    /// <summary>
    /// Verdadeiro quando o corpo traz ao menos um controle. Um corpo vazio é
    /// erro de cliente, não um no-op silencioso.
    /// </summary>
    public bool HasAnyChange =>
        VulnMode.HasValue || VerboseErrors.HasValue || RateLimit.HasValue || SecurityHeaders.HasValue;
}
