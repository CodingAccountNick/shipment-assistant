using Microsoft.AspNetCore.Mvc;
using Shipments.Api.Services;


namespace Shipments.Api.Controllers
{


    [ApiController]
    [Route("api/[controller]")]
    public class AssistantController(AiServiceClient ai) : ControllerBase
    {

            // POST api/assistant/ask
            [HttpPost("ask")]
            public async Task<ActionResult<AskResponse>> Ask(AskRequest request, CancellationToken ct)
            {
                if (string.IsNullOrWhiteSpace(request.Question))
                    return BadRequest("Question cannot be empty");

                try
                {
                    var answer = await ai.AskAsync(request.Question, ct);
                    return Ok(new AskResponse(answer));
                }
                catch (HttpRequestException)
                {
                    // Python service is down or returned an error
                    return StatusCode(503, "AI service is not available");
                }
            }
        }
   
}
