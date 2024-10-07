using Alejandria.Server.Models;
using Alejandria.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alejandria.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        //private readonly GenerateResponseRAG _generateResponseRAG;
        private readonly ChatService _chatOpenAIService;

        public ChatController(ChatService service)
        {
            _chatOpenAIService = service;
            //_generateResponseRAG = responseRAG;
        }

        [HttpPost("PostMessage")]
        public async Task<ActionResult> PostMessage([FromBody] ChatRequest request)
        {
            var response = await _chatOpenAIService.GetResponse(request.History, request.Query);
            request.History.Add(response);
            return Ok(request.History);
        }
    }
}
