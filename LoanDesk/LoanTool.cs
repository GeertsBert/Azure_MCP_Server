using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace LoanDesk;

public record ItemInfo(string Id, string Name, string Category, string? BorrowedBy, DateTimeOffset? BorrowedOn);

public class LoanTools(LoanRepository repository, ILogger<LoanTools> logger)
{
    [Function(nameof(ListItems))]
    public async Task<IReadOnlyList<ItemInfo>> ListItems(
        [McpToolTrigger("list_items",
            "Lists the items of the loan desk with their id and category, and who has borrowed them. " +
            "An item without borrowedBy is available.")]
            ToolInvocationContext context,
        [McpToolProperty("category", "Optional. Only show items of this category.")]
            Category? category)
    {
        logger.LogInformation("list_items called with category {Category}", category);
        var items = await repository.GetItemsAsync(category);
        return items
            .Select(i => new ItemInfo(i.RowKey, i.Name, i.PartitionKey, i.BorrowedBy, i.BorrowedOn))
            .ToList();
    }

    private const int MaxItemsPerBorrower = 2;

    [Function(nameof(BorrowItem))]
    public Task<string> BorrowItem(
        [McpToolTrigger("borrow_item",
            "Lends an item of the loan desk to a person. " +
            "A person can borrow at most 2 items at the same time. Use list_items to find all items and their ids.")]
            ToolInvocationContext context,
        [McpToolProperty("borrower", "The name of the person who borrows the item.", isRequired: true)]
            string borrower,
        [McpToolProperty("itemId", "The id of the item to borrow, for example LAP-01. Not case sensitive.", isRequired: true)]
            string itemId) =>
        RunSafely("borrow_item", async () =>
        {
            logger.LogInformation("borrow_item called with borrower {Borrower} and itemId {Item Id}", borrower, itemId);
            borrower = borrower?.Trim() ?? "";
            itemId = itemId?.Trim() ?? "";
            if (borrower.Length == 0)
                return "Please provide the name of the borrower.";
            if (itemId.Length == 0)
                return "Please provide an item id. Use list_items to find one.";
            var items = await repository.GetItemsAsync();
            var item = FindItem(items, itemId);

            if (item is null)
                return $"Item '{itemId}' does not exist. Use list_items to see which all items.";
            if (!string.IsNullOrEmpty(item.BorrowedBy))
                return $"{item.RowKey} ({item.Name}) is already borrowed by {item.BorrowedBy} " +
                       $"since {item.BorrowedOn:yyyy-MM-dd HH:mm} UTC.";
            var alreadyBorrowed = items
                .Where(i => string.Equals(i.BorrowedBy, borrower, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (alreadyBorrowed.Count >= MaxItemsPerBorrower)
                return $"{borrower} already has {alreadyBorrowed.Count} items " +
                       $"({string.Join(", ", alreadyBorrowed.Select(i => i.RowKey))}). "+
                       $"The maximum is {MaxItemsPerBorrower}. Return an item before borrowing a new one.";
            item.BorrowedBy = borrower;
            item.BorrowedOn = DateTimeOffset.UtcNow;
            try
            {
                await repository.SaveAsync(item);
            }
            catch (RequestFailedException e) when (e.Status == 412)
            {
                return $"Someone else borrowed {item.RowKey} a moment ago. Use list_items to see available items.";
            }
            return $"{borrower} has borrowed {item.RowKey} ({item.Name}) on {item.BorrowedOn:yyyy-MM-dd HH:mm} UTC.";
        });

    [Function(nameof(ReturnItem))]
    public Task<string> ReturnItem(
        [McpToolTrigger("return_item",
            "Returns a borrowed item to the loan desk so it becomes available again. Use list_items first to see all items and their ids.")]
            ToolInvocationContext context,
        [McpToolProperty("itemId", "The id of the item to return, for example LAP-01. Not case sensitive.", isRequired: true)]
            string itemId) =>
        RunSafely("return_item", async () =>
        {
            logger.LogInformation("return_item called with itemId {ItemId}", itemId);

            itemId = itemId?.Trim() ?? "";
            if (itemId.Length == 0)
                return "Please provide an item id. Use list_items to find one.";

            var items = await repository.GetItemsAsync();

            var item = FindItem(items, itemId);
            if (item is null)
                return $"Item '{itemId}' does not exist. Use list_items to see which items exist and their ids.";

            if (string.IsNullOrEmpty(item.BorrowedBy))
                return $"{item.RowKey} ({item.Name}) is not borrowed, so it cannot be returned. It is already available.";

            var borrower = item.BorrowedBy;
            var borrowedOn = item.BorrowedOn;
            item.BorrowedBy = null;
            item.BorrowedOn = null;
            try
            {
                await repository.SaveAsync(item);
            }
            catch (RequestFailedException e) when (e.Status == 412)
            {
                return $"Someone else returned {item.RowKey} a moment ago.";
            }

            return $"{borrower} has returned {item.RowKey} ({item.Name}), borrowed since {borrowedOn:yyyy-MM-dd HH:mm} UTC. It is available again.";
        });

    // Item ids are matched case-insensitively, so "lap-01" finds "LAP-01".
    private static LoanItem? FindItem(IEnumerable<LoanItem> items, string itemId) =>
        items.FirstOrDefault(i => string.Equals(i.RowKey, itemId, StringComparison.OrdinalIgnoreCase));

    // Runs a tool and turns any unexpected exception into a sentence, so the client never receives an exception.
    private async Task<string> RunSafely(string toolName, Func<Task<string>> tool)
    {
        try
        {
            return await tool();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Tool} failed", toolName);
            return $"Sorry, {toolName} could not be completed because of a technical problem with the loan desk. Please try again later.";
        }
    }
}
