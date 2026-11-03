using System.Globalization;
using System.Text.RegularExpressions;
using CalculatorLibrary;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace CalculatorProgram;

public sealed record VoiceCalculationResult(
    bool Success,
    double Result,
    string Message);

public sealed class VoiceCalculator
{
    private readonly SpeechConfig _speechConfig;

    public VoiceCalculator(string speechKey, string speechRegion, string language = "en-US")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(speechKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(speechRegion);

        _speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        _speechConfig.SpeechRecognitionLanguage = language;
    }

    public async Task<VoiceCalculationResult> CalculateFromMicrophoneAsync(
        Calculator calculator,
        CancellationToken cancellationToken = default)
    {
        using AudioConfig audio = AudioConfig.FromDefaultMicrophoneInput();
        using SpeechRecognizer recognizer = new(_speechConfig, audio);

        AddCalculatorPhrases(recognizer);

        Console.WriteLine("Say a calculation, for example: 'add 12 and 30'.");
        Console.WriteLine("Listening...");

        SpeechRecognitionResult speechResult = await recognizer.RecognizeOnceAsync()
            .ConfigureAwait(false);

        if (speechResult.Reason != ResultReason.RecognizedSpeech)
        {
            return new VoiceCalculationResult(
                false,
                double.NaN,
                GetRecognitionErrorMessage(speechResult));
        }

        string utterance = NormalizeTranscript(speechResult.Text);
        Console.WriteLine($"Heard: {utterance}");

        return TryCalculate(calculator, utterance);
    }

    private static void AddCalculatorPhrases(SpeechRecognizer recognizer)
    {
        var phraseList = PhraseListGrammar.FromRecognizer(recognizer);

        string[] phrases =
        [
            "add",
            "plus",
            "subtract",
            "minus",
            "multiply",
            "multiplied by",
            "times",
            "divide",
            "divided by",
            "power",
            "raised to",
            "square root",
            "sine",
            "cosine",
            "tangent",
            "calculate"
        ];

        foreach (string phrase in phrases)
        {
            phraseList.AddPhrase(phrase);
        }

        phraseList.SetWeight(1.5);
    }

    private static VoiceCalculationResult TryCalculate(
        Calculator calculator,
        string utterance)
    {
        string normalized = utterance.ToLowerInvariant();

        List<double> numbers = ExtractNumbers(normalized);

        if (numbers.Count == 0)
        {
            return new VoiceCalculationResult(
                false,
                double.NaN,
                "I could not find a number in your command.");
        }

        try
        {
            if (ContainsAny(normalized, "add", "plus", "+") && numbers.Count >= 2)
            {
                return Complete(calculator.DoOperation(numbers[0], numbers[1], "a"));
            }

            if (ContainsAny(normalized, "subtract", "minus", "-") && numbers.Count >= 2)
            {
                return Complete(calculator.DoOperation(numbers[0], numbers[1], "s"));
            }

            if (ContainsAny(normalized, "multiply", "times", "multiplied by", "x") &&
                numbers.Count >= 2)
            {
                return Complete(calculator.DoOperation(numbers[0], numbers[1], "m"));
            }

            if (ContainsAny(normalized, "divide", "divided by", "/") && numbers.Count >= 2)
            {
                return Complete(calculator.DoOperation(numbers[0], numbers[1], "d"));
            }

            if (ContainsAny(normalized, "power", "raised to") && numbers.Count >= 2)
            {
                return Complete(calculator.DoOperation(numbers[0], numbers[1], "p"));
            }

            if (ContainsAny(normalized, "square root", "sqrt"))
            {
                return Complete(calculator.DoOperation(numbers[0], "r"));
            }

            if (ContainsAny(normalized, "sine", "sin"))
            {
                return Complete(calculator.DoOperation(numbers[0], "sin"));
            }

            if (ContainsAny(normalized, "cosine", "cos"))
            {
                return Complete(calculator.DoOperation(numbers[0], "cos"));
            }

            if (ContainsAny(normalized, "tangent", "tan"))
            {
                return Complete(calculator.DoOperation(numbers[0], "tan"));
            }

            return new VoiceCalculationResult(
                false,
                double.NaN,
                "I recognized the speech but not a supported calculation.");
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            return new VoiceCalculationResult(
                false,
                double.NaN,
                $"Unable to save the calculation: {exception.Message}");
        }
    }

    private static string NormalizeTranscript(string transcript)
    {
        string normalized = transcript.Trim().ToLowerInvariant();

        // Azure sometimes transcribes spoken whole numbers as clock times,
        // for example "twelve" as "12:00".
        normalized = Regex.Replace(normalized, @"\b(\d+):00\b", "$1");

        // Common misrecognition: "At 12 and 30" instead of "Add 12 and 30".
        // Only replace "at" when it appears where an operation is expected.
        normalized = Regex.Replace(
            normalized,
            @"(?:^|\s)at\s+(?=\d)",
            " add ",
            RegexOptions.CultureInvariant);

        // Collapse repeated whitespace created by the replacements.
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    private static VoiceCalculationResult Complete(double result)
    {
        return double.IsNaN(result)
            ? new VoiceCalculationResult(
                false,
                double.NaN,
                "That calculation is undefined or mathematically invalid.")
            : new VoiceCalculationResult(
                true,
                result,
                $"Your result: {result:G}");
    }

    private static List<double> ExtractNumbers(string utterance)
    {
        List<double> numbers = [];

        foreach (Match match in Regex.Matches(
            utterance,
            @"(?<![\d:.])-?\d+(?:\.\d+)?(?![\d:])",
            RegexOptions.CultureInvariant))
        {
            if (double.TryParse(
                    match.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number) &&
                double.IsFinite(number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    private static bool ContainsAny(string utterance, params string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (utterance.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetRecognitionErrorMessage(SpeechRecognitionResult result)
    {
        return result.Reason switch
        {
            ResultReason.NoMatch => "Speech was not recognized.",
            ResultReason.Canceled => GetCancellationMessage(result),
            _ => $"Speech recognition failed: {result.Reason}."
        };
    }

    private static string GetCancellationMessage(SpeechRecognitionResult result)
    {
        CancellationDetails cancellation = CancellationDetails.FromResult(result);

        return cancellation.Reason switch
        {
            CancellationReason.Error =>
                $"Speech recognition failed: {cancellation.ErrorDetails}",
            CancellationReason.EndOfStream => "The microphone stream ended.",
            _ => $"Speech recognition was canceled: {cancellation.Reason}."
        };
    }
}

