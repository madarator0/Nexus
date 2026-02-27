using EventTaskManager.Application.Interface;
using EventTaskManager.Application.TaskEvent.Test;
using Microsoft.AspNetCore.Mvc;

namespace EventTaskManager.Controllers;

[ApiController]
[Route("test")]
public class TestController : ControllerBase
{
    [HttpPost("test")] 
    public async Task<IActionResult> Test(IEventBus bus)
    {
        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid()));
        return Ok();
    }
}
