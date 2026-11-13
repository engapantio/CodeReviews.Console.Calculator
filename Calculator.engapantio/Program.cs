using CalculatorLibrary;

namespace CalculatorProgram;

internal static class Program
{
    async private static Task Main()
    {
        string envPath = Path.Combine(
    AppContext.BaseDirectory,
    ".env");
        DotNetEnv.Env.Load();

        Console.WriteLine("Console Calculator in C#");
        Console.WriteLine("------------------------");

        Calculator calculator;
        try
        {
            calculator = new Calculator();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.WriteLine($"Unable to load calculator history: {exception.Message}");
            return;
        }

        var endApp = false;
        while (!endApp)
        {
            Console.WriteLine($"\nSuccessful calculations: {calculator.CalculationCount}");
            Console.WriteLine("Choose an option:");
            Console.WriteLine("\tc - Calculate");
            Console.WriteLine("\th - View calculation history");
            Console.WriteLine("\td - Delete calculation history");
            Console.WriteLine("\tv - Calculate by voice");
            Console.WriteLine("\tq - Quit");
            Console.Write("Your option? ");

            switch (Console.ReadLine()?.Trim().ToLowerInvariant())
            {
                case "c":
                    Calculate(calculator);
                    break;
                case "h":
                    DisplayHistory(calculator);
                    break;
                case "d":
                    ClearHistory(calculator);
                    break;
                case "v":
                    await CalculateByVoiceAsync(calculator);
                    break;
                case "q":
                case null:
                    endApp = true;
                    break;
                default:
                    Console.WriteLine("Unrecognized option.");
                    break;
            }
        }

        try
        {
            calculator.Finish();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Unable to save calculator history: {exception.Message}");
        }
    }

    private static void Calculate(Calculator calculator)
    {
        Console.WriteLine("\nChoose an operation:");
        Console.WriteLine("\ta - Add");
        Console.WriteLine("\ts - Subtract");
        Console.WriteLine("\tm - Multiply");
        Console.WriteLine("\td - Divide");
        Console.WriteLine("\tp - Power (x^y)");
        Console.WriteLine("\tr - Square root");
        Console.WriteLine("\tx - 10^x");
        Console.WriteLine("\tsin - Sine (degrees)");
        Console.WriteLine("\tcos - Cosine (degrees)");
        Console.WriteLine("\ttan - Tangent (degrees)");
        Console.Write("Your option? ");

        string? operation = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (operation is null)
        {
            return;
        }

        var isUnary = operation is "r" or "x" or "sin" or "cos" or "tan";
        var isBinary = operation is "a" or "s" or "m" or "d" or "p";
        if (!isUnary && !isBinary)
        {
            Console.WriteLine("Unrecognized operation.");
            return;
        }

        if (!TryReadOperand(calculator, "Enter the first value (or H to use a history result): ", out double first))
        {
            return;
        }

        double result;
        try
        {
            if (isUnary)
            {
                result = calculator.DoOperation(first, operation);
            }
            else
            {
                if (!TryReadOperand(calculator, "Enter the second value (or H to use a history result): ", out double second))
                {
                    return;
                }

                result = calculator.DoOperation(first, second, operation);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            Console.WriteLine($"Unable to save the calculation: {exception.Message}");
            return;
        }

        if (double.IsNaN(result))
        {
            Console.WriteLine("This operation is undefined or will result in a mathematical error.");
        }
        else
        {
            Console.WriteLine($"Your result: {result:G}");
        }
    }

    private static async Task CalculateByVoiceAsync(Calculator calculator)
    {
        string? speechKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        string? speechRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");
        string? speechLanguage = Environment.GetEnvironmentVariable("AZURE_SPEECH_LANGUAGE");

        if (string.IsNullOrWhiteSpace(speechKey) ||
            string.IsNullOrWhiteSpace(speechRegion))
        {
            Console.WriteLine(
                "Voice calculation is not configured. " +
                "Set AZURE_SPEECH_KEY and AZURE_SPEECH_REGION in .env.");
            return;
        }

        try
        {
            var voiceCalculator = new VoiceCalculator(
                speechKey,
                speechRegion,
                speechLanguage ?? "en-US");

            VoiceCalculationResult result =
                await voiceCalculator.CalculateFromMicrophoneAsync(calculator);

            Console.WriteLine(result.Message);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                InvalidOperationException or
                ApplicationException or
                ObjectDisposedException)
        {
            Console.WriteLine($"Voice calculation failed: {exception.Message}");
            Console.WriteLine("You can continue using manual input.");
        }
    }


    private static bool TryReadOperand(Calculator calculator, string prompt, out double value)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();
            if (input is null)
            {
                value = 0;
                return false;
            }

            input = input.Trim();
            if (input.Equals("h", StringComparison.OrdinalIgnoreCase))
            {
                if (calculator.History.Count == 0)
                {
                    Console.WriteLine("There are no saved results to use.");
                    continue;
                }

                DisplayHistory(calculator);
                Console.Write("Enter the history number to use: ");
                string? selection = Console.ReadLine();
                if (int.TryParse(selection, out int historyNumber) &&
                    historyNumber >= 1 &&
                    historyNumber <= calculator.History.Count)
                {
                    value = calculator.History[calculator.History.Count - historyNumber].Result;
                    return true;
                }

                if (selection is null)
                {
                    value = 0;
                    return false;
                }

                Console.WriteLine("That is not a valid history number.");
                continue;
            }

            if (double.TryParse(input, out value) && double.IsFinite(value))
            {
                return true;
            }

            Console.WriteLine("Invalid number. Enter a finite numeric value or H.");
        }
    }

    private static void DisplayHistory(Calculator calculator)
    {
        Console.WriteLine($"\nSuccessful calculations: {calculator.CalculationCount}");
        if (calculator.History.Count == 0)
        {
            Console.WriteLine("No recent calculations.");
            return;
        }

        Console.WriteLine("Recent calculations (most recent first):");
        for (int index = calculator.History.Count - 1; index >= 0; index--)
        {
            CalculationEntry entry = calculator.History[index];
            int historyNumber = calculator.History.Count - index;
            string expression = entry.Operand2 is double operand2
                ? $"{entry.Operand1:G} {entry.Operation} {operand2:G}"
                : $"{entry.Operation}({entry.Operand1:G})";
            Console.WriteLine($"{historyNumber}. {expression} = {entry.Result:G}");
        }
    }

    private static void ClearHistory(Calculator calculator)
    {
        try
        {
            calculator.ClearHistory();
            Console.WriteLine("Calculation history deleted. The lifetime calculation count is unchanged.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Unable to delete calculation history: {exception.Message}");
        }
    }
}
