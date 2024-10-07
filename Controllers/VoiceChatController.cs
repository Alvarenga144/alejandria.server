using Alejandria.Server.Models;
using Alejandria.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alejandria.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VoiceChatController : ControllerBase
    {
        //private readonly GenerateResponseRAG _generateResponseRAG;
        private readonly VoiceChatService _VoiceChatOpenAIService;

        public VoiceChatController(VoiceChatService service)
        {
            _VoiceChatOpenAIService = service;
            //_generateResponseRAG = responseRAG;
        }

        [HttpPost("TextPrompt")]
        public async Task<ActionResult> PostMessage([FromBody] ChatRequest request)
        {
            var response = await _VoiceChatOpenAIService.GetResponse(request.History, request.Query);
            request.History.Add(response);
            return Ok(request.History);
        }
    }
}
