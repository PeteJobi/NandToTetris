namespace VMTranslator
{
    public class HackTranslator
    {
        public record Command(Action Action, Segment? Segment = null, SegmentProps? SegmentProps = null, short? Operand = null);
        public record SegmentProps(string Name, string? Address);

        private async Task<List<Command>> Parse(string vmPath)
        {
            var commandsWithWhitespace = await File.ReadAllLinesAsync(vmPath);
            var commands = new List<Command>();
            short variableCount = 0;

            //Process whitespace and comments
            var commandLines = commandsWithWhitespace.Where(comm =>
            {
                var trimmedComm = comm.TrimStart();
                return trimmedComm != string.Empty && !trimmedComm.StartsWith("//");
            });

            foreach (var commandLine in commandLines)
            {
                var components = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                switch (components[0])
                {
                    case "push" or "pop":
                        var segment = _segmentsDic[components[1]];
                        var operand = short.Parse(components[2]);
                        var segmentAddress = segment switch
                        {
                            Segment.TEMP => "R5",
                            Segment.STTC => "16",
                            _ => null
                        };
                        commands.Add(new Command(components[0] == "push" ? Action.PUSH : Action.POP, segment, new SegmentProps(components[1], segmentAddress), operand));
                        break;
                    default:
                        commands.Add(new Command(_actionsDic[components[0]]));
                        break;
                }
            }

            return commands;
        }

        private async Task Write(List<Command> commands, string vmPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(vmPath);
            var parentFolder = Path.GetDirectoryName(vmPath);
            var outputFile = Path.Join(parentFolder, fileName) + ".asm";

            using (var streamWriter = File.CreateText(outputFile))
            {
                for (var i = 0; i < commands.Count; i++)
                {
                    var command = commands[i];
                    string asmCode;
                    switch (command.Action)
                    {
                        case Action.PUSH:
                            switch (command.Segment)
                            {
                                case Segment.CNST:
                                    asmCode = $"""
                                               // Push constant {command.Operand}
                                               @{command.Operand}
                                               D=A
                                               @SP
                                               A=M
                                               M=D
                                               @SP
                                               M=M+1

                                               """;
                                    break;
                                case Segment.PTR:
                                    asmCode = $"""
                                               // Push pointer {command.Operand}
                                               @{(command.Operand == 0 ? Segment.THIS : Segment.THAT)}
                                               D=M
                                               @SP
                                               A=M
                                               M=D
                                               @SP
                                               M=M+1

                                               """;
                                    break;
                                default:
                                    asmCode = $"""
                                               // Push {command.SegmentProps?.Name} {command.Operand}
                                               @{command.SegmentProps?.Address ?? command.Segment.ToString()}
                                               D={(command.SegmentProps?.Address == null ? "M" : "A")}
                                               @{command.Operand}
                                               A=D+A
                                               D=M
                                               @SP
                                               A=M
                                               M=D
                                               @SP
                                               M=M+1

                                               """;
                                    break;
                            }
                            break;
                        case Action.POP:
                            switch (command.Segment)
                            {
                                case Segment.CNST:
                                    throw new InvalidOperationException("You cannot pop to a constant.");
                                case Segment.PTR:
                                    asmCode = $"""
                                               // Pop pointer {command.Operand}
                                               @SP
                                               M=M-1
                                               A=M
                                               D=M
                                               @{(command.Operand == 0 ? Segment.THIS : Segment.THAT)}
                                               M=D
                                               """;
                                    break;
                                default:
                                    asmCode = $"""
                                               // Pop {command.SegmentProps?.Name} {command.Operand}
                                               @{command.SegmentProps?.Address ?? command.Segment.ToString()}
                                               D={(command.SegmentProps?.Address == null ? "M" : "A")}
                                               @{command.Operand}
                                               D=D+A
                                               @R14
                                               M=D
                                               @SP
                                               M=M-1
                                               A=M
                                               D=M
                                               @R14
                                               A=M
                                               M=D

                                               """;
                                    break;
                            }
                            break;
                        case Action.ADD:
                            asmCode = """
                                      // Add
                                      @SP
                                      M=M-1
                                      A=M
                                      D=M
                                      @SP
                                      A=M-1
                                      M=D+M

                                      """;
                            break;
                        case Action.SUB:
                            asmCode = """
                                      // Subtract
                                      @SP
                                      M=M-1
                                      A=M
                                      D=M
                                      @SP
                                      A=M-1
                                      M=M-D

                                      """;
                            break;
                        case Action.AND:
                            asmCode = """
                                      // And
                                      @SP
                                      M=M-1
                                      A=M
                                      D=M
                                      @SP
                                      A=M-1
                                      M=D&M

                                      """;
                            break;
                        case Action.OR:
                            asmCode = """
                                      // Or
                                      @SP
                                      M=M-1
                                      A=M
                                      D=M
                                      @SP
                                      A=M-1
                                      M=D|M

                                      """;
                            break;
                        case Action.EQ:
                            asmCode = $"""
                                       // Equal
                                       @SP
                                       M=M-1
                                       A=M
                                       D=M
                                       @SP
                                       A=M-1
                                       D=M-D
                                       @TRUE_{i}
                                       D;JEQ
                                       @SP
                                       A=M-1
                                       M=0
                                       @CONT_{i}
                                       0;JMP
                                       (TRUE_{i})
                                       @SP
                                       A=M-1
                                       M=-1
                                       (CONT_{i})

                                       """;
                            break;
                        case Action.GT:
                            asmCode = $"""
                                       // Greater
                                       @SP
                                       M=M-1
                                       A=M
                                       D=M
                                       @SP
                                       A=M-1
                                       D=M-D
                                       @TRUE_{i}
                                       D;JGT
                                       @SP
                                       A=M-1
                                       M=0
                                       @CONT_{i}
                                       0;JMP
                                       (TRUE_{i})
                                       @SP
                                       A=M-1
                                       M=-1
                                       (CONT_{i})

                                       """;
                            break;
                        case Action.LT:
                            asmCode = $"""
                                       // Less
                                       @SP
                                       M=M-1
                                       A=M
                                       D=M
                                       @SP
                                       A=M-1
                                       D=M-D
                                       @TRUE_{i}
                                       D;JLT
                                       @SP
                                       A=M-1
                                       M=0
                                       @CONT_{i}
                                       0;JMP
                                       (TRUE_{i})
                                       @SP
                                       A=M-1
                                       M=-1
                                       (CONT_{i})

                                       """;
                            break;
                        case Action.NEG:
                            asmCode = """
                                      // Negate
                                      @SP
                                      A=M-1
                                      M=-M

                                      """;
                            break;
                        case Action.NOT:
                            asmCode = """
                                      // Not
                                      @SP
                                      A=M-1
                                      M=!M

                                      """;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    await streamWriter.WriteLineAsync(asmCode);
                }
            }
        }

        public async Task Translate(string vmPath)
        {
            var commands = await Parse(vmPath);
            await Write(commands, vmPath);
        }

        private readonly Dictionary<string, Segment> _segmentsDic = new()
        {
            { "constant", Segment.CNST },
            { "local", Segment.LCL },
            { "argument", Segment.ARG },
            { "this", Segment.THIS },
            { "that", Segment.THAT },
            { "temp", Segment.TEMP },
            { "static", Segment.STTC },
            { "pointer", Segment.PTR }
        };

        private readonly Dictionary<string, Action> _actionsDic = new()
        {
            { "add", Action.ADD },
            { "sub", Action.SUB },
            { "neg", Action.NEG },
            { "eq", Action.EQ },
            { "gt", Action.GT },
            { "lt", Action.LT },
            { "and", Action.AND },
            { "or", Action.OR },
            { "not", Action.NOT }
        };
    }

    public enum Action
    {
        PUSH,
        POP,
        ADD,
        SUB,
        NEG,
        EQ,
        GT,
        LT,
        AND,
        OR,
        NOT
    }

    public enum Segment
    {
        LCL,
        ARG,
        THIS,
        THAT,
        TEMP,
        STTC,
        PTR,
        CNST
    }
}
