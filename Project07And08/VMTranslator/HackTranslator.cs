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
                    var asmCode = command.Action switch
                    {
                        Action.PUSH => command.Segment switch
                        {
                            Segment.CNST => $"""
                                             // Push constant {command.Operand}
                                             @{command.Operand}
                                             D=A
                                             @SP
                                             A=M
                                             M=D
                                             @SP
                                             M=M+1

                                             """,
                            Segment.PTR => $"""
                                            // Push pointer {command.Operand}
                                            @{(command.Operand == 0 ? Segment.THIS : Segment.THAT)}
                                            D=M
                                            @SP
                                            A=M
                                            M=D
                                            @SP
                                            M=M+1

                                            """,
                            _ => $"""
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

                                  """
                        },
                        Action.POP => command.Segment switch
                        {
                            Segment.CNST => throw new InvalidOperationException("You cannot pop to a constant."),
                            Segment.PTR => $"""
                                            // Pop pointer {command.Operand}
                                            @SP
                                            M=M-1
                                            A=M
                                            D=M
                                            @{(command.Operand == 0 ? Segment.THIS : Segment.THAT)}
                                            M=D
                                            """,
                            _ => $"""
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

                                  """
                        },
                        Action.ADD => """
                                      // Add
                                      @SP
                                      M=M-1
                                      A=M
                                      D=M
                                      @SP
                                      A=M-1
                                      M=D+M

                                      """,
                        Action.SUB => """
                                      // Subtract
                                      @SP
                                      M=M-1
                                      A=M
                                      D=M
                                      @SP
                                      A=M-1
                                      M=M-D

                                      """,
                        Action.AND => """
                                      // And
                                      @SP
                                      M=M-1
                                      A=M
                                      D=M
                                      @SP
                                      A=M-1
                                      M=D&M

                                      """,
                        Action.OR => """
                                     // Or
                                     @SP
                                     M=M-1
                                     A=M
                                     D=M
                                     @SP
                                     A=M-1
                                     M=D|M

                                     """,
                        Action.EQ => $"""
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

                                      """,
                        Action.GT => $"""
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

                                      """,
                        Action.LT => $"""
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

                                      """,
                        Action.NEG => """
                                      // Negate
                                      @SP
                                      A=M-1
                                      M=-M

                                      """,
                        Action.NOT => """
                                      // Not
                                      @SP
                                      A=M-1
                                      M=!M

                                      """,
                        _ => throw new ArgumentOutOfRangeException()
                    };

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
