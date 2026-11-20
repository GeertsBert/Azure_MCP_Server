using Azure;
using Azure.Data.Tables;

namespace LoanDesk;

public enum Category { Laptop, SensorKit, Camera }

public class LoanItem : ITableEntity
{
    public string PartitionKey { get; set; } = "";   // the category
    public string RowKey { get; set; } = "";         // the item id, for example LAP-01
    public string Name { get; set; } = "";
    public string? BorrowedBy { get; set; }
    public DateTimeOffset? BorrowedOn { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

public class LoanRepository(TableClient table)
{
    private static readonly (Category Category, string Id, string Name)[] Seed =
    [
        (Category.Laptop, "LAP-01", "Dell Latitude 5450"),
        (Category.Laptop, "LAP-02", "MacBook Air M3"),
        (Category.SensorKit, "KIT-01", "Raspberry Pi 5 sensor kit"),
        (Category.SensorKit, "KIT-02", "ESP32 starter kit"),
        (Category.Camera, "CAM-01", "Sony ZV-E10"),
        (Category.Camera, "CAM-02", "GoPro Hero 12"),
    ];

    public async Task<List<LoanItem>> GetItemsAsync(Category? category = null)
    {
        await EnsureSeededAsync();
        string? filter = category is null
            ? null
            : TableClient.CreateQueryFilter($"PartitionKey eq {category.ToString()}");

        var items = new List<LoanItem>();
        await foreach (var item in table.QueryAsync<LoanItem>(filter))
            items.Add(item);
        return items;
    }

    // Fails with status 412 when somebody else changed the item since you read it.
    public Task SaveAsync(LoanItem item) =>
        table.UpdateEntityAsync(item, item.ETag, TableUpdateMode.Replace);

    private async Task EnsureSeededAsync()
    {
        await table.CreateIfNotExistsAsync();
        await foreach (var _ in table.QueryAsync<LoanItem>(maxPerPage: 1))
            return;                                   // the table already has items

        foreach (var (category, id, name) in Seed)
            await table.AddEntityAsync(new LoanItem
            { PartitionKey = category.ToString(), RowKey = id, Name = name });
    }
}