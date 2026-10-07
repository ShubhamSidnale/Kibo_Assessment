namespace InventoryHold.Infrastructure;

public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "";

    public string Database { get; set; } = "inventory_hold";
}

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = "";

    public string KeyPrefix { get; set; } = "inventory-hold:";
}

public sealed class RabbitOptions
{
    public string ConnectionString { get; set; } = "";

    public string Exchange { get; set; } = "inventory.holds";

    public string Queue { get; set; } = "inventory.holds.events";
}
