using System;
using System.Collections.Generic;
using System.Text;

namespace EventTaskManager.Application.Interface;

public interface IEventBus
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class, IIntegrationEvent;
}
