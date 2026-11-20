using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace Company.Function;

public class HelloMCP
{
    private ILogger<HelloMCP> _logger;

    public HelloMCP(ILogger<HelloMCP> logger)
    {
        _logger = logger;
    }

    [Function(nameof(SayHello))]
    public string SayHello(
        [McpToolTrigger("say_hello",
            "Greets a person by name. Use this when the user asks you to greet or welcome someone.")]
            ToolInvocationContext context,
        [McpToolProperty("name", "The first name of the person to greet.", isRequired: true)]
            string name)
    {
        _logger.LogInformation("say_hello called with name {Name}", name);
        return $"Hello {name}, greetings from Azure Functions!";
    }

    [Function(nameof(GetServerTime))]
    public string GetServerTime(
        [McpToolTrigger("get_server_time",
            "Returns the current local date and time on the server, e.g. 'Friday 20 November 2026, 09:17:18'. Use this when the user asks what the date or time is on the server.")]
            ToolInvocationContext context)
    {
        _logger.LogInformation("get_server_time called");
        return DateTime.Now.ToString("dddd d MMMM yyyy, HH:mm:ss");
    }

    [Function(nameof(CalculateVat))]
    public string CalculateVat(
        [McpToolTrigger("calculate_vat",
            "Calculates the VAT for an amount excluding VAT. Use this when the user asks how much VAT is due or what the total price including VAT is.")]
            ToolInvocationContext context,
        [McpToolProperty("amount", "The amount excluding VAT, e.g. 100.", isRequired: true)]
            decimal amount,
        [McpToolProperty("vatPercentage", "The VAT rate as a percentage, e.g. 21 for 21%.", isRequired: true)]
            decimal vatPercentage)
    {
        _logger.LogInformation("calculate_vat called with amount {Amount} and vatPercentage {VatPercentage}", amount, vatPercentage);

        if (amount < 0)
        {
            return "Error: amount must be 0 or greater.";
        }

        if (vatPercentage < 0 || vatPercentage > 100)
        {
            return "Error: vatPercentage must be between 0 and 100.";
        }

        var vat = Math.Round(amount * vatPercentage / 100, 2, MidpointRounding.AwayFromZero);
        var total = amount + vat;

        return $"Amount excl. VAT: {amount:0.00} | VAT ({vatPercentage:0.##}%): {vat:0.00} | Total incl. VAT: {total:0.00}";
    }
}
