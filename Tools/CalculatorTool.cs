using System;

public class CalculatorTool
{
    public string Name => "calculator";

    public string Description =>
        "Performs a basic arithmetic operation (add, subtract, multiply, divide) on two numbers.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            operation = new
            {
                type = "string",
                @enum = new[] { "add", "subtract", "multiply", "divide" },
                description = "The arithmetic operation to perform",
            },
            a = new { type = "number", description = "The first operand" },
            b = new { type = "number", description = "The second operand" },
        },
        required = new[] { "operation", "a", "b" },
    };

    public double Execute(string operation, double a, double b)
    {
        return operation switch
        {
            "add" => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide" => b != 0
                ? a / b
                : throw new DivideByZeroException("Cannot divide by zero."),
            _ => throw new ArgumentException($"Unknown operation: {operation}", nameof(operation)),
        };
    }
}
