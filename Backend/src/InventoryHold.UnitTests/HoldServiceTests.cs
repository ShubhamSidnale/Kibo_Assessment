using InventoryHold.Contracts;
using InventoryHold.Domain;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Services;
using Moq;
using Xunit;

namespace InventoryHold.UnitTests;

public sealed class HoldServiceTests
{
    private readonly Mock<IHoldRepository> repository = new(MockBehavior.Strict);

    private readonly Mock<IInventoryCache> cache = new(MockBehavior.Strict);

    private readonly FixedClock clock = new();

    private HoldService Service => new(repository.Object, cache.Object, new HoldOptions(), clock);

    private Hold Active => new("11111111111111111111111111111111", [new("keyboard", 2)], HoldStatus.Active,
        clock.Now, clock.Now.AddMinutes(15));

    [Fact]
    public async Task Empty_items_are_rejected_before_persistence()
    {
        DomainException error = await Assert.ThrowsAsync<DomainException>(() => Service.CreateAsync(new([])));

        Assert.Equal(400, error.StatusCode);

        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Nonpositive_quantities_are_rejected(int quantity)
    {
        await Assert.ThrowsAsync<DomainException>(() => Service.CreateAsync(new([new("keyboard", quantity)])));

        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Duplicate_products_are_rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() => Service.CreateAsync(new([new("keyboard", 1), new("keyboard",
            2)])));

        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Null_item_is_rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() => Service.CreateAsync(new([null!])));
    }

    [Fact]
    public async Task Creation_uses_configured_expiration_and_active_status()
    {
        Hold? persisted = null;

        repository.Setup(r => r.CreateAsync(It.IsAny<Hold>(), It.IsAny<CancellationToken>())).Callback<Hold,
            CancellationToken>((h, _) => persisted = h).ReturnsAsync((Hold h, CancellationToken _) => h);

        HoldService service = new HoldService(repository.Object, cache.Object, new HoldOptions
        {
            DurationMinutes =
            7
        }, clock);

        HoldResponse result = await service.CreateAsync(new([new("keyboard", 2)]));

        Assert.Equal(HoldStatus.Active, result.Status);

        Assert.Equal(clock.Now.AddMinutes(7), result.ExpiresAt);

        Assert.Equal(persisted!.Id, result.Id);

        Assert.True(Guid.TryParseExact(result.Id, "N", out _));
    }

    [Fact]
    public async Task Stock_conflict_is_propagated_without_caching()
    {
        repository.Setup(r => r.CreateAsync(It.IsAny<Hold>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new DomainException("insufficient_stock",
                "Not enough stock.", 409));

        DomainException error = await Assert.ThrowsAsync<DomainException>(() =>
            Service.CreateAsync(new([new("keyboard", 100)])));

        Assert.Equal(409, error.StatusCode);

        cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_hold_returns_not_found()
    {
        repository.Setup(r => r.GetAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Hold?)null);

        DomainException error = await Assert.ThrowsAsync<DomainException>(() => Service.GetAsync("missing"));

        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public async Task Reading_expired_active_hold_performs_expiration()
    {
        Hold expired = Active with
        {
            ExpiresAt = clock.Now
        };

        repository.Setup(r => r.GetAsync(expired.Id, It.IsAny<CancellationToken>())).ReturnsAsync(expired);

        repository.Setup(r => r.EndAsync(expired.Id, HoldStatus.Expired, clock.Now,
            It.IsAny<CancellationToken>())).ReturnsAsync(expired with
            {
                Status = HoldStatus.Expired,
                EndedAt =
                clock.Now
            });

        HoldResponse result = await Service.GetAsync(expired.Id);

        Assert.Equal(HoldStatus.Expired, result.Status);

        repository.Verify(r => r.EndAsync(expired.Id, HoldStatus.Expired, clock.Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Reading_unexpired_hold_does_not_restore_stock()
    {
        repository.Setup(r => r.GetAsync(Active.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Active);

        Assert.Equal(HoldStatus.Active, (await Service.GetAsync(Active.Id)).Status);

        repository.Verify(r => r.EndAsync(It.IsAny<string>(), It.IsAny<HoldStatus>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(HoldStatus.Released)]
    [InlineData(HoldStatus.Expired)]
    public async Task Release_returns_existing_terminal_status(HoldStatus status)
    {
        Hold ended = Active with
        {
            Status = status,
            EndedAt = clock.Now
        };

        repository.Setup(r => r.EndAsync(ended.Id, HoldStatus.Released, clock.Now,
            It.IsAny<CancellationToken>())).ReturnsAsync(ended);

        Assert.Equal(status, (await Service.ReleaseAsync(ended.Id)).Status);
    }

    [Fact]
    public async Task Releasing_missing_hold_returns_not_found()
    {
        repository.Setup(r => r.EndAsync("missing", HoldStatus.Released, clock.Now,
            It.IsAny<CancellationToken>())).ReturnsAsync((Hold?)null);

        DomainException error = await Assert.ThrowsAsync<DomainException>(() => Service.ReleaseAsync("missing"));

        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public async Task Cache_hit_avoids_inventory_query()
    {
        repository.Setup(r => r.GetInventoryRevisionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5);

        cache.Setup(c => c.GetAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new InventorySnapshot(5,
            [new("keyboard", "Keyboard", 25)]));

        Assert.Equal(25, (await Service.InventoryAsync())[0].AvailableQuantity);

        repository.Verify(r => r.GetInventoryAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Concurrent_mutation_during_cache_fill_retries_with_new_revision()
    {
        repository.SetupSequence(r =>
            r.GetInventoryRevisionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1).ReturnsAsync(2).ReturnsAsync
                (2).ReturnsAsync(2);

        cache.Setup(c => c.GetAsync(It.IsAny<long>(),
            It.IsAny<CancellationToken>())).ReturnsAsync((InventorySnapshot?)null);

        repository.SetupSequence(r =>
            r.GetInventoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new("keyboard", "Keyboard",
                25)]).ReturnsAsync([new("keyboard", "Keyboard", 23)]);

        cache.Setup(c => c.SetAsync(It.Is<InventorySnapshot>(s => s.Revision == 2 &&
            s.Products[0].AvailableQuantity == 23), TimeSpan.FromSeconds(30),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Assert.Equal(23, (await Service.InventoryAsync())[0].AvailableQuantity);

        cache.Verify(c => c.SetAsync(It.Is<InventorySnapshot>(s => s.Revision == 1), It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Expiration_sweep_ends_each_due_hold()
    {
        repository.Setup(r => r.FindExpiredAsync(clock.Now, 100,
            It.IsAny<CancellationToken>())).ReturnsAsync([Active.Id]);

        repository.Setup(r => r.EndAsync(Active.Id, HoldStatus.Expired, clock.Now,
            It.IsAny<CancellationToken>())).ReturnsAsync(Active with { Status = HoldStatus.Expired });

        await Service.ExpireAsync();

        repository.Verify(r => r.EndAsync(Active.Id, HoldStatus.Expired, clock.Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Invalid_pagination_is_rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() => Service.ListAsync(null, null, 101));

        await Assert.ThrowsAsync<DomainException>(() => Service.ListAsync(null, "bad-cursor", 50));
    }
}

internal sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; } = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}
