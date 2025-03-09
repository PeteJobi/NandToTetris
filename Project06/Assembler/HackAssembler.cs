namespace Assembler
{
    public class HackAssembler
    {
        private const short VARIABLE_START = 16;

        public async Task<Result> Assemble(string asmPath)
        {
            var symbolTable = new Dictionary<string, short>(PredefinedSymbols);
            if (!File.Exists(asmPath)) return new Result { IsSuccessful = false, ErrorMessage = "File does not exists" };
            var instructionsWithWhitespace = await File.ReadAllLinesAsync(asmPath);
            var machineCode = new List<string>();
            short variableCount = 0;

            //Process whitespace and comments
            var instructions = instructionsWithWhitespace.Where(ins =>
            {
                var trimmedInstr = ins.TrimStart();
                return trimmedInstr != string.Empty && !trimmedInstr.StartsWith("//");
            });

            //Collect labels
            var labelCount = 0;
            for (var i = 0; i < instructions.Count(); i++)
            {
                var instruction = instructions.ElementAt(i).Trim();
                if (!instruction.StartsWith('(')) continue;
                symbolTable.Add(instruction[1..^1], (short)(i - labelCount++));
            }

            for (var i = 0; i < instructions.Count(); i++)
            {
                var instruction = instructions.ElementAt(i).Trim();
                if (instruction.StartsWith('(')) continue;
                if (instruction.StartsWith('@')) //A-instruction
                {
                    var afterAt = instruction[1..];
                    short afterAtValue = 0;
                    if (symbolTable.TryGetValue(afterAt, out var value)) afterAtValue = value;
                    else if (short.TryParse(afterAt, out var result)) afterAtValue = result;
                    else
                    {
                        afterAtValue = (short)(VARIABLE_START + variableCount);
                        symbolTable.Add(afterAt, afterAtValue);
                        variableCount++;
                    }

                    var afterAtValueBinary = afterAtValue.ToString("b15");
                    machineCode.Add("0" + afterAtValueBinary);
                }
                else //C-instruction
                {
                    var destinationBinary = "000";
                    var jumpBinary = "000";

                    var components = instruction.Split('=', StringSplitOptions.TrimEntries);
                    if (components.Length > 1)
                    {
                        destinationBinary = components[0] switch
                        {
                            "M" => "001",
                            "D" => "010",
                            "MD" => "011",
                            "A" => "100",
                            "AM" => "101",
                            "AD" => "110",
                            "AMD" => "111",
                            _ => "000"
                        };
                    }

                    components = components.Last().Split(';', StringSplitOptions.TrimEntries);
                    if (components.Length > 1)
                    {
                        jumpBinary = components[1] switch
                        {
                            "JGT" => "001",
                            "JEQ" => "010",
                            "JGE" => "011",
                            "JLT" => "100",
                            "JNE" => "101",
                            "JLE" => "110",
                            "JMP" => "111",
                            _ => "000"
                        };
                    }

                    var computation = components.First();
                    var a = computation.Contains('M') ? "1" : "0";
                    computation = computation.Replace('M', 'A');
                    var computationBinary = computation switch
                    {
                        "0" => "101010",
                        "1" => "111111",
                        "-1" => "111010",
                        "D" => "001100",
                        "A" => "110000",
                        "!D" => "001101",
                        "!A" => "110001",
                        "-D" => "001111",
                        "-A" => "110011",
                        "D+1" => "011111",
                        "A+1" => "110111",
                        "D-1" => "001110",
                        "A-1" => "110010",
                        "D+A" => "000010",
                        "D-A" => "010011",
                        "A-D" => "000111",
                        "D&A" => "000000",
                        "D|A" => "010101",
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    machineCode.Add("111" + a + computationBinary + destinationBinary + jumpBinary);
                }
            }

            var fileName = Path.GetFileNameWithoutExtension(asmPath);
            var parentFolder = Path.GetDirectoryName(asmPath);
            var outputFile = Path.Join(parentFolder, fileName) + ".hack";
            await File.WriteAllLinesAsync(outputFile, machineCode);

            return new Result { IsSuccessful = true };
        }

        private static Dictionary<string, short> PredefinedSymbols => new()
        {
            {"R0", 0},
            {"R1", 1},
            {"R2", 2},
            {"R3", 3},
            {"R4", 4},
            {"R5", 5},
            {"R6", 6},
            {"R7", 7},
            {"R8", 8},
            {"R9", 9},
            {"R10", 10},
            {"R11", 11},
            {"R12", 12},
            {"R13", 13},
            {"R14", 14},
            {"R15", 15},
            {"SCREEN", 16384},
            {"KBD", 24576},
            {"SP", 0},
            {"LCL", 1},
            {"ARG", 2},
            {"THIS", 3},
            {"THAT", 4}
        };

        public class Result
        {
            public bool IsSuccessful { get; set; }
            public string ErrorMessage { get; set; }
        }
    }
}
