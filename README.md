# Console Calculator with Voice Input

A .NET 8 console application for performing arithmetic and scientific calculations, viewing saved calculation history, and entering commands by voice using Azure Speech.

## Features

- Basic arithmetic:
  - Add
  - Subtract
  - Multiply
  - Divide
  - Power
- Unary operations:
  - Square root
  - 10^x
  - Sine
  - Cosine
  - Tangent
- Persistent calculation history
- History limit of 10 entries
- Voice-driven calculation using a microphone and Azure Speech
- Supports reusing previous results from the history list as operands

## Project structure

- `Calculator.engapantio/`
  - Console app entry point
  - Voice recognition logic
  - Menu-driven UI
- `CalculatorLibrary/`
  - Shared calculator logic
  - Calculation history persistence
  - Mathematical operations

## Requirements

- .NET 8 SDK
- An Azure Speech resource
- Microphone access for voice input
- Optional: local `.env` file with speech credentials

## Setup

1. Restore dependencies:

```bash
dotnet restore
```

2. Create a `.env` file in the `Calculator.engapantio` project folder:

```env
AZURE_SPEECH_KEY=your_speech_key
AZURE_SPEECH_REGION=eastus
AZURE_SPEECH_LANGUAGE=en-US
```

> The project is configured to copy `.env` to the build output folder.

3. Run the app:

```bash
dotnet run --project Calculator.engapantio/Calculator.engapantio.csproj
```

## Application menu

When the app starts, it presents a menu:

- `c` - Calculate
- `h` - View calculation history
- `d` - Delete calculation history
- `v` - Calculate by voice
- `q` - Quit

## Manual calculation examples

The calculator supports:

- Add: `a`
- Subtract: `s`
- Multiply: `m`
- Divide: `d`
- Power: `p`
- Square root: `r`
- 10^x: `x`
- Sine: `sin`
- Cosine: `cos`
- Tangent: `tan`

Example:

```text
Enter the first value: 12
Enter the second value: 5
```

Then choose:

```text
m
```

Result:

```text
Your result: 60
```

## Voice commands

The voice calculator listens for spoken phrases and maps them to operations. Example phrases include:

- "add 12 and 30"
- "subtract 20 minus 7"
- "multiply 4 times 5"
- "divide 100 divided by 4"
- "power 2 raised to 8"
- "square root of 81"
- "sine 30"
- "cosine 60"
- "tangent 45"

The app normalizes common speech recognition issues and attempts to interpret phrases such as:

- "At 12 and 30" → interpreted as "add 12 and 30"
- "twelve" recognized as `12:00` is corrected back to `12`

## History behavior

The app keeps a running history of successful calculations and stores them in:

```text
calculatorlog.json
```

- Up to 10 entries are retained
- The most recent calculations are preserved
- History entries may be reused as operands by selecting `H` during input

## Notes

- Voice input depends on Azure Speech credentials being valid.
- If Azure Speech is not configured, the app will inform you that voice calculation is not available.
- Invalid or undefined operations return a clear error message instead of crashing.

## License

This project is intended for educational and personal use. Add a license if you plan to distribute it publicly.
