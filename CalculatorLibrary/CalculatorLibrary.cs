using System.Globalization;
using System.Text.Json;

namespace CalculatorLibrary;

public sealed record CalculationEntry(
    double Operand1,
    double? Operand2,
    string Operation,
    double Result);

public class Calculator
{
    private const int HistoryLimit = 10;
    private readonly string _logPath;
    private readonly List<CalculationEntry> _operations;

    public Calculator(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(Environment.CurrentDirectory, "calculatorlog.json");
        (_operations, CalculationCount) = LoadState(_logPath);
    }

    public int CalculationCount { get; private set; }

    public IReadOnlyList<CalculationEntry> History => _operations;

    public double DoOperation(double num1, double num2, string op)
    {
        if (!double.IsFinite(num1) || !double.IsFinite(num2))
        {
            return double.NaN;
        }

        (double result, string operation) = op switch
        {
            "a" => (num1 + num2, "Add"),
            "s" => (num1 - num2, "Subtract"),
            "m" => (num1 * num2, "Multiply"),
            "d" => (num2 == 0 ? double.NaN : num1 / num2, "Divide"),
            "p" => (Math.Pow(num1, num2), "Power"),
            _ => (double.NaN, string.Empty)
        };

        return RecordCalculation(num1, num2, operation, result);
    }

    public double DoOperation(double num1, string op)
    {
        if (!double.IsFinite(num1))
        {
            return double.NaN;
        }

        (double result, string operation) = op switch
        {
            "r" => (num1 < 0 ? double.NaN : Math.Sqrt(num1), "Square Root"),
            "x" => (Math.Pow(10, num1), "10^x"),
            "sin" => (Math.Sin(ToRadians(num1)), "Sine"),
            "cos" => (Math.Cos(ToRadians(num1)), "Cosine"),
            "tan" => (IsTangentUndefined(num1) ? double.NaN : Math.Tan(ToRadians(num1)), "Tangent"),
            _ => (double.NaN, string.Empty)
        };

        return RecordCalculation(num1, null, operation, result);
    }

    public void ClearHistory()
    {
        SaveState([], CalculationCount);
        _operations.Clear();
    }

    public void Finish()
    {
        SaveState(_operations, CalculationCount);
    }

    private double RecordCalculation(double firstOperand, double? secondOperand, string operation, double result)
    {
        if (operation.Length == 0 || !double.IsFinite(result))
        {
            return double.NaN;
        }

        var updatedOperations = new List<CalculationEntry>(_operations)
        {
            new(firstOperand, secondOperand, operation, result)
        };

        if (updatedOperations.Count > HistoryLimit)
        {
            updatedOperations.RemoveAt(0);
        }

        int updatedCount = checked(CalculationCount + 1);
        SaveState(updatedOperations, updatedCount);
        _operations.Clear();
        _operations.AddRange(updatedOperations);
        CalculationCount = updatedCount;
        return result;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static bool IsTangentUndefined(double degrees)
    {
        double normalizedDegrees = Math.Abs(degrees % 180);
        return Math.Abs(normalizedDegrees - 90) < 1e-12;
    }

    private static (List<CalculationEntry> Operations, int Count) LoadState(string logPath)
    {
        if (!File.Exists(logPath))
        {
            return ([], 0);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(logPath));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(root, "Operations", out JsonElement operationsElement) ||
                operationsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Calculator history file '{logPath}' has an invalid format.");
            }

            List<CalculationEntry> operations = [];
            foreach (JsonElement element in operationsElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !TryGetProperty(element, "Operand1", out JsonElement operand1Element) ||
                    !TryReadNumber(operand1Element, out double operand1) ||
                    !TryGetProperty(element, "Operation", out JsonElement operationElement) ||
                    operationElement.ValueKind != JsonValueKind.String ||
                    !TryGetProperty(element, "Result", out JsonElement resultElement) ||
                    !TryReadNumber(resultElement, out double result))
                {
                    throw new InvalidDataException($"Calculator history file '{logPath}' contains an invalid calculation.");
                }

                double? operand2 = null;
                if (TryGetProperty(element, "Operand2", out JsonElement operand2Element) &&
                    operand2Element.ValueKind != JsonValueKind.Null)
                {
                    if (!TryReadNumber(operand2Element, out double parsedOperand2))
                    {
                        throw new InvalidDataException($"Calculator history file '{logPath}' contains an invalid calculation.");
                    }

                    operand2 = parsedOperand2;
                }

                string operation = operationElement.GetString()!;
                if (string.IsNullOrWhiteSpace(operation) || !double.IsFinite(operand1) ||
                    operand2 is double value && !double.IsFinite(value))
                {
                    throw new InvalidDataException($"Calculator history file '{logPath}' contains an invalid calculation.");
                }

                // Older logs record failed calculations with non-finite results such as "NaN".
                if (double.IsFinite(result))
                {
                    operations.Add(new CalculationEntry(operand1, operand2, operation, result));
                }
            }

            int count = operations.Count;
            if (TryGetProperty(root, "Count", out JsonElement countElement) &&
                (!countElement.TryGetInt32(out count) || count < 0))
            {
                throw new InvalidDataException($"Calculator history file '{logPath}' has an invalid calculation count.");
            }

            return (operations.TakeLast(HistoryLimit).ToList(), Math.Max(count, operations.Count));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Calculator history file '{logPath}' contains invalid JSON.", exception);
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryReadNumber(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        value = 0;
        return false;
    }

    private void SaveState(IReadOnlyList<CalculationEntry> operations, int count)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(_logPath))!;
        Directory.CreateDirectory(directory);

        string temporaryPath = _logPath + ".tmp";
        try
        {
            string json = JsonSerializer.Serialize(
                new CalculatorState { Count = count, Operations = operations.ToList() },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _logPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed class CalculatorState
    {
        public int Count { get; set; }

        public List<CalculationEntry> Operations { get; set; } = [];
    }
}
