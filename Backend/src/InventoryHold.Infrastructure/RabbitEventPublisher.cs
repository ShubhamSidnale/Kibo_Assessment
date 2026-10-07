using System.Text.Json;
using InventoryHold.Contracts;
using InventoryHold.Domain.Repositories;
using RabbitMQ.Client;

namespace InventoryHold.Infrastructure;

public sealed class RabbitEventPublisher(RabbitOptions options) : IEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private IConnection? connection;

    private IChannel? channel;

    public async Task PublishAsync(HoldLifecycleEvent message, CancellationToken ct)
    {
        await gate.WaitAsync(ct);

        try
        {
            if (connection is null || !connection.IsOpen)
            {
                if (channel is not null)
                {
                    await channel.DisposeAsync();
                }

                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }

                connection = await new ConnectionFactory
                {
                    Uri = new Uri(options.ConnectionString),
                    AutomaticRecoveryEnabled = true
                }.CreateConnectionAsync(ct);

                channel = null;
            }

            if (channel is null || !channel.IsOpen)
            {
                if (channel is not null)
                {
                    await channel.DisposeAsync();
                }

                channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled
                    : true, publisherConfirmationTrackingEnabled: true), ct);

                await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true,
                    autoDelete: false, cancellationToken: ct);

                await channel.QueueDeclareAsync(options.Queue, durable: true, exclusive: false, autoDelete: false,
                    cancellationToken: ct);

                await channel.QueueBindAsync(options.Queue, options.Exchange, "hold.*", cancellationToken: ct);
            }

            string routingKey = message.EventType switch
            {
                "HoldCreated" => "hold.created",
                "HoldReleased" => "hold.released",
                "HoldExpired" => "hold.expired",
                _ => throw new ArgumentException("Unknown lifecycle event.", nameof(message))
            };

            await channel.BasicPublishAsync(options.Exchange, routingKey, mandatory: true,
                basicProperties: new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = message.EventId,
                    Type = message.EventType
                },
                        body: JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web),
                            cancellationToken: ct);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (channel is not null)
        {
            await channel.DisposeAsync();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        gate.Dispose();
    }
}
