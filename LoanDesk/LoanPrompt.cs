using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace LoanDesk;

public class LoanPrompts
{
    [Function(nameof(EquipmentForProject))]
    public string EquipmentForProject(
        [McpPromptTrigger("equipment_for_project",
            Title = "Equipment for a project",
            Description = "Suggests which items to borrow for a project, and borrows them after you agree.")]
            PromptInvocationContext context,
        [McpPromptArgument("project", "What is the project about?", isRequired: true)] string? project,
        [McpPromptArgument("borrower", "Who borrows the items?", isRequired: true)] string? borrower)
    {
        return $"""
            I am starting this project: {project}.
            Look at the loan desk with list_items and suggest which available items would help, with one reason each.
            Ask me which ones I want before you borrow anything.
            Then borrow them for {borrower} with borrow_item.
            """;
    }
}