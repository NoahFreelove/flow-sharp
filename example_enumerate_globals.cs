using FlowLang.Core;
using FlowLang.Runtime;

// Example: Enumerating global variables from Flow code

var engine = new FlowEngine();

// Execute some Flow code that creates global variables
engine.Execute(@"
    Int myNumber = 42
    String myText = ""Hello""
    Bool myFlag = true

    use ""@audio""
    DEFAULT_SAMPLE_RATE = 48000
", "example.flow");

Console.WriteLine("=== All Global Variables ===");

// Get all variables from the global frame
var allGlobals = engine.Context.GlobalFrame.GetAllAccessibleVariables();

foreach (var (name, value) in allGlobals)
{
    Console.WriteLine($"{name}: {value.Type.Name} = {value}");
}

Console.WriteLine();
Console.WriteLine("=== Fetch Specific Variable by Name ===");

// Fetch a specific variable by name
var sampleRate = engine.Context.GlobalFrame.GetVariable("DEFAULT_SAMPLE_RATE");
int rate = sampleRate.As<int>();
Console.WriteLine($"Sample Rate: {rate}");

// Get local variables only (excludes parent scopes)
Console.WriteLine();
Console.WriteLine("=== Local Variables Only ===");
var localVars = engine.Context.GlobalFrame.GetLocalVariables();
foreach (var (name, value) in localVars)
{
    Console.WriteLine($"{name}: {value.Type.Name} = {value}");
}
