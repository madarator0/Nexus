using BackoffBus.RabbitMQ.Configuration;

namespace BackoffBus.Tests;

public sealed class RabbitMqBackoffBusOptionsTests
{
    [Fact]
    public void Validate_DefaultOptions_AreValid()
    {
        var options = new RabbitMqBackoffBusOptions();

        options.Validate();
    }

    [Fact]
    public void Validate_BatchMinimumAboveMaximum_Throws()
    {
        var options = new RabbitMqBackoffBusOptions
        {
            PublisherBatchSize = 10,
            PublisherBatchMinimumSize = 11
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            options.Validate);
    }

    [Fact]
    public void Validate_UnknownBucketSelection_Throws()
    {
        var options = new RabbitMqBackoffBusOptions
        {
            DelayBucketSelection =
                (RabbitMqDelayBucketSelection)int.MaxValue
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            options.Validate);
    }
}
