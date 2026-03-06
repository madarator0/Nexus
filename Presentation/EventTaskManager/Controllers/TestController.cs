using EventTaskManager.Application.Interface;
using EventTaskManager.Application.TaskEvent.Test;
using Microsoft.AspNetCore.Mvc;

namespace EventTaskManager.Controllers;

[ApiController]
[Route("test")]
public class TestController : ControllerBase
{
    [HttpPost("test")]
    public async Task<IActionResult> Test(IEventBus bus, ILogger<TestController> logger)
    {
        var now = DateTime.UtcNow;

        logger.LogInformation("Start {Now}", now);

        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), "1")
        {
            ExecuteAfter = now.AddSeconds(15)
        });

        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), "2")
        {
            ExecuteAfter = now.AddSeconds(5)
        });

        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), "3")
        {
            ExecuteAfter = now.AddSeconds(10)
        });


        return Ok(new
        {
            Message = "Events scheduled",
            Time = now
        });
    }
}