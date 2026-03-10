using EventTaskManager.Application.Interface;
using EventTaskManager.Application.TaskEvent.Test;
using Microsoft.AspNetCore.Mvc;

namespace EventTaskManager.Controllers;

[ApiController]
[Route("test")]
public class TestController : ControllerBase
{
    [HttpPost("test/1")]
    public async Task<IActionResult> Test1(IEventBus bus, ILogger<TestController> logger)
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

    [HttpPost("test/2")]
    public async Task<IActionResult> Test2(IEventBus bus, ILogger<TestController> logger)
    {
        var now = DateTime.UtcNow;
        logger.LogInformation("Start {Now}", now);
        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), "4")
        {
            ExecuteAfter = now.AddSeconds(5)
        });
        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), "5")
        {
            ExecuteAfter = now.AddSeconds(30)
        });
        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), "6")
        {
            ExecuteAfter = now.AddSeconds(107)
        });
        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), "7")
        {
            ExecuteAfter = now.AddSeconds(132)
        });
        return Ok(new
        {
            Message = "Event scheduled",
            Time = now
        });
    }
}