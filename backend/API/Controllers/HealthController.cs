using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Verificação de saúde da API. Não requer autenticação.
/// Usado pelo Docker Compose (healthcheck) e pelo frontend.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() =>
        Ok(new { status = "healthy", service = "cyberprotech-api", time = DateTime.UtcNow });
}
