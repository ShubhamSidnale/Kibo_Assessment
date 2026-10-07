using InventoryHold.Contracts;
using InventoryHold.Domain;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Services;
using Moq;
using Xunit;

namespace InventoryHold.UnitTests;

public sealed class OutboxDispatcherTests
{
    private readonly Mock<IOutboxRepository> repository = new(MockBehavior.Strict);

    private readonly Mock<IEventPublisher> publisher = new(MockBehavior.Strict);

    private readonly FixedClock clock = new();

    private OutboxMessage? message;

    private OutboxMessage Message => message ??= new("event-1", new("event-1", "HoldCreated", "hold-1",
        [new("keyboard", 1)], HoldStatus.Active, clock.Now, clock.Now, clock.Now.AddMinutes(15)));

    [Fact]
    public async Task Confirmed_event_is_marked_delivered_after_publish()
    {
        MockSequence sequence = new MockSequence();

        repository.InSequence(sequence).Setup(r => r.ClaimAsync(It.IsAny<string>(), clock.Now,
            It.IsAny<CancellationToken>())).ReturnsAsync(Message);

        publisher.InSequence(sequence).Setup(p => p.PublishAsync(Message.Event,
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        repository.InSequence(sequence).Setup(r => r.CompleteAsync(Message.Id, It.IsAny<string>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Assert.True(await new OutboxDispatcher(repository.Object, publisher.Object, clock).DispatchOneAsync());

        repository.VerifyAll();

        publisher.VerifyAll();
    }

    [Fact]
    public async Task Broker_failure_retries_event_without_marking_it_delivered()
    {
        repository.Setup(r => r.ClaimAsync(It.IsAny<string>(), clock.Now,
            It.IsAny<CancellationToken>())).ReturnsAsync(Message);

        publisher.Setup(p => p.PublishAsync(Message.Event,
            It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Broker unavailable"));

        repository.Setup(r => r.RetryAsync(Message.Id, It.IsAny<string>(), clock.Now.AddSeconds(10),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<IOException>(() => new OutboxDispatcher(repository.Object, publisher.Object,
            clock).DispatchOneAsync());

        repository.Verify(r => r.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);

        repository.Verify(r => r.RetryAsync(Message.Id, It.IsAny<string>(), clock.Now.AddSeconds(10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Empty_outbox_does_not_publish()
    {
        repository.Setup(r => r.ClaimAsync(It.IsAny<string>(), clock.Now,
            It.IsAny<CancellationToken>())).ReturnsAsync((OutboxMessage?)null);

        Assert.False(await new OutboxDispatcher(repository.Object, publisher.Object, clock).DispatchOneAsync());

        publisher.VerifyNoOtherCalls();
    }
}
