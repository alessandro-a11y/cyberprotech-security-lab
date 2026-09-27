using Application.DTOs;
using Application.Interfaces;
using API.Infrastructure;
using Infrastructure.Lab;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Controles do laboratório de segurança.
/// </summary>
/// <remarks>
/// <para>
/// Este controller é o <b>contrato</b> que o frontend consome; a lógica dos
/// cenários vulneráveis é da Sprint 5. Nesta sprint os quatro controles já
/// existem, ficam legíveis e graváveis, mas nada se ancora atrás deles ainda —
/// a API segue inteira no estado corrigido. Isso é intencional: o estado
/// reportado é o estado real, e ele é "corrigido" porque o modo vulnerável
/// ainda não foi implementado.
/// </para>
/// <para>
/// Dupla trava em <see cref="LabState.Writable"/>: os controles só mudam com
/// <c>Lab:Enabled=true</c> <b>e</b> em ambiente de desenvolvimento. Deploy com a
/// configuração padrão não expõe o modo vulnerável.
/// </para>
/// </remarks>
[ApiController]
[Route("api/lab")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Authenticated)]
public sealed class LabController(LabState labState) : ControllerBase
{
    /// <summary>
    /// Estado dos controles do laboratório. Usado pela tela de Administração
    /// para mostrar a posição real dos toggles em vez de um valor fixo no código.
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType<LabConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LabConfigResponse> GetConfig() =>
        Ok(new LabConfigResponse(
            labState.Enabled,
            IsWritable(),
            labState.GetToggles()));

    /// <summary>
    /// Altera os controles enviados. Exclusivo de Admin, e só com o laboratório
    /// habilitado em desenvolvimento.
    /// </summary>
    [HttpPut("config")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<LabConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public ActionResult<LabConfigResponse> UpdateConfig([FromBody] LabConfigUpdateRequest request)
    {
        if (!labState.Enabled)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Laboratório desabilitado. Defina Lab:Enabled=true no ambiente.",
            });
        }

        if (!IsWritable())
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Os controles do laboratório só podem ser alterados em ambiente de desenvolvimento.",
            });
        }

        if (!request.HasAnyChange)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Informe ao menos um controle para alterar.",
            });
        }

        labState.Apply(request);

        return Ok(new LabConfigResponse(labState.Enabled, IsWritable(), labState.GetToggles()));
    }

    /// <summary>
    /// Lê e grava são sempre permitidos a autenticados; só a gravação depende da
    /// dupla trava.
    /// </summary>
    private bool IsWritable() => labState.Writable;
}
