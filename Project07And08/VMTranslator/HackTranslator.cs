using static System.IO.Path;

namespace VMTranslator
{
    public class HackTranslator
    {
        private async Task<List<ICommand>> Parse(string[] vmFiles)
        {
            var commands = new List<ICommand>();

            var currentStaticAddress = 16; //static values begin at R16
            foreach (var vmFile in vmFiles)
            {
                using (var streamReader = File.OpenText(vmFile))
                {
                    string? commandLine;
                    var staticAllocation = 0;
                    while ((commandLine = await streamReader.ReadLineAsync()) != null)
                    {
                        var trimmedCon = commandLine.TrimStart();
                        if (trimmedCon == string.Empty || trimmedCon.StartsWith("//")) continue; //Exclude whitespace and comments
                        var components = commandLine.Split("//")[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        switch (components[0])
                        {
                            case "push" or "pop":
                                var segment = _segmentsDic[components[1]];
                                var operand = short.Parse(components[2]);
                                var segmentAddress = segment switch
                                {
                                    Segment.TEMP => "R5",
                                    Segment.STTC => currentStaticAddress.ToString(),
                                    _ => null
                                };
                                if (segment == Segment.STTC && staticAllocation < operand + 1) staticAllocation = operand + 1;
                                commands.Add(new PushPopCommand
                                {
                                    Action = components[0] == "push" ? Action.PUSH : Action.POP, 
                                    Operand = operand, 
                                    Segment = segment, 
                                    SegmentProps = new SegmentProps(components[1], segmentAddress )
                                });
                                break;
                            case "label":
                                commands.Add(new LabelCommand
                                {
                                    Action = Action.LABEL, 
                                    Name = components[1]
                                });
                                break;
                            case "goto":
                                commands.Add(new LabelCommand
                                {
                                    Action = Action.GOTO, 
                                    Name = components[1]
                                });
                                break;
                            case "if-goto":
                                commands.Add(new LabelCommand
                                {
                                    Action = Action.IFGOTO, 
                                    Name = components[1]
                                });
                                break;
                            case "function":
                                commands.Add(new FunctionCommand
                                {
                                    Action = Action.FUNC,
                                    Name = components[1],
                                    Allocation = short.Parse(components[2])
                                });
                                break;
                            case "call":
                                commands.Add(new FunctionCommand
                                {
                                    Action = Action.CALL,
                                    Name = components[1],
                                    Allocation = short.Parse(components[2])
                                });
                                break;
                            default:
                                commands.Add(new BasicCommand { Action = _actionsDic[components[0]] });
                                break;
                        }
                    }
                    currentStaticAddress += staticAllocation;
                }
            }

            return commands;
        }

        private async Task Write(List<ICommand> commands, string outputFile, bool hasSys)
        {
            using (var streamWriter = File.CreateText(outputFile))
            {
                if (hasSys)
                {
                    const string callSysInit = """
                                                //Initialize system
                                                @256
                                                D=A
                                                @0
                                                M=D //Set SP to 256
                                                @300
                                                D=A
                                                @1
                                                M=D //Set LCL to 300
                                                @400
                                                D=A
                                                @2
                                                M=D //Set ARG to 400
                                                @3000
                                                D=A
                                                @3
                                                M=D //Set THIS to 3000
                                                @4000
                                                D=A
                                                @4
                                                M=D //Set THAT to 4000
                                                
                                                """;
                    await streamWriter.WriteLineAsync(callSysInit);
                    commands = commands.Prepend(new FunctionCommand { Action = Action.CALL, Name = "Sys.init", Allocation = 0 }).ToList(); //Call Sys.init
                }
                FunctionCommand? currentFunction = null;
                for (var i = 0; i < commands.Count; i++)
                {
                    var command = commands[i];
                    if(command.Action == Action.FUNC) currentFunction = command as FunctionCommand;
                    var asmCode = command switch
                    {
                        PushPopCommand pushPopCommand => pushPopCommand.Action switch
                        {
                            Action.PUSH => pushPopCommand.Segment switch
                            {
                                Segment.CNST => $"""
                                                 // Push constant {pushPopCommand.Operand}
                                                 @{pushPopCommand.Operand}
                                                 D=A
                                                 @SP
                                                 A=M
                                                 M=D
                                                 @SP
                                                 M=M+1

                                                 """,
                                Segment.PTR => $"""
                                                // Push pointer {pushPopCommand.Operand}
                                                @{(pushPopCommand.Operand == 0 ? Segment.THIS : Segment.THAT)}
                                                D=M
                                                @SP
                                                A=M
                                                M=D
                                                @SP
                                                M=M+1

                                                """,
                                _ => $"""
                                      // Push {pushPopCommand.SegmentProps?.Name} {pushPopCommand.Operand}
                                      @{pushPopCommand.SegmentProps?.Address ?? pushPopCommand.Segment.ToString()}
                                      D={(pushPopCommand.SegmentProps?.Address == null ? "M" : "A")}
                                      @{pushPopCommand.Operand}
                                      A=D+A
                                      D=M
                                      @SP
                                      A=M
                                      M=D
                                      @SP
                                      M=M+1

                                      """
                            },
                            Action.POP => pushPopCommand.Segment switch
                            {
                                Segment.CNST => throw new InvalidOperationException("You cannot pop to a constant."),
                                Segment.PTR => $"""
                                                // Pop pointer {pushPopCommand.Operand}
                                                @SP
                                                AM=M-1
                                                D=M
                                                @{(pushPopCommand.Operand == 0 ? Segment.THIS : Segment.THAT)}
                                                M=D
                                                """,
                                _ => $"""
                                      // Pop {pushPopCommand.SegmentProps?.Name} {pushPopCommand.Operand}
                                      @{pushPopCommand.SegmentProps?.Address ?? pushPopCommand.Segment.ToString()}
                                      D={(pushPopCommand.SegmentProps?.Address == null ? "M" : "A")}
                                      @{pushPopCommand.Operand}
                                      D=D+A
                                      @R14
                                      M=D
                                      @SP
                                      AM=M-1
                                      D=M
                                      @R14
                                      A=M
                                      M=D

                                      """
                            },
                            _ => throw new ArgumentOutOfRangeException()
                        },
                        BasicCommand basicCommand => basicCommand.Action switch
                        {
                            Action.ADD => """
                                      // Add
                                      @SP
                                      AM=M-1
                                      D=M
                                      @SP
                                      A=M-1
                                      M=D+M

                                      """,
                            Action.SUB => """
                                      // Subtract
                                      @SP
                                      AM=M-1
                                      D=M
                                      @SP
                                      A=M-1
                                      M=M-D

                                      """,
                            Action.AND => """
                                      // And
                                      @SP
                                      AM=M-1
                                      D=M
                                      @SP
                                      A=M-1
                                      M=D&M

                                      """,
                            Action.OR => """
                                     // Or
                                     @SP
                                     AM=M-1
                                     D=M
                                     @SP
                                     A=M-1
                                     M=D|M

                                     """,
                            Action.EQ => $"""
                                      // Equal
                                      @SP
                                      AM=M-1
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
                                      AM=M-1
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
                                      AM=M-1
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
                            Action.RET => $"""
                                           //return ({currentFunction?.Name ?? throw new InvalidOperationException()})
                                           @LCL
                                           A=M-1
                                           D=M
                                           @THAT //Restore THAT
                                           M=D
                                           @2
                                           D=A
                                           @LCL
                                           A=M-D
                                           D=M
                                           @THIS //Restore THIS
                                           M=D
                                           @LCL
                                           A=M-1
                                           D=M
                                           @4
                                           D=A
                                           @LCL //Set LCL to where saved THAT was
                                           M=M-1
                                           A=M-D
                                           D=M
                                           @LCL //Put return address at LCL
                                           A=M
                                           M=D
                                           @2
                                           D=A
                                           @LCL //Set LCL to where saved THIS was
                                           M=M-1
                                           A=M-D
                                           D=M
                                           @LCL //Put saved LCL at current LCL
                                           A=M
                                           M=D
                                           @SP
                                           A=M-1
                                           D=M
                                           @ARG //Put return value in first ARG
                                           A=M
                                           M=D
                                           D=A+1
                                           @SP //Set SP to address after first ARG
                                           M=D
                                           @LCL
                                           A=M+1
                                           D=M
                                           @SP //Put return address at SP
                                           A=M
                                           M=D
                                           @LCL
                                           A=M-1
                                           D=M
                                           @ARG //Restore ARG
                                           M=D
                                           @LCL
                                           A=M
                                           D=M
                                           @LCL //Restore LCL
                                           M=D
                                           @SP //Jump to return address
                                           A=M
                                           A=M
                                           0;JMP
                                           
                                           """,
                            _ => throw new ArgumentOutOfRangeException()
                        },
                        LabelCommand labelCommand => labelCommand.Action switch
                        {
                            Action.LABEL => $"({labelCommand.Name})",
                            Action.GOTO => $"""
                                            // goto {labelCommand.Name}
                                            @{labelCommand.Name}
                                            0;JMP

                                            """,
                            Action.IFGOTO => $"""
                                              // if-goto {labelCommand.Name}
                                              @SP
                                              AM=M-1
                                              D=M
                                              @{labelCommand.Name}
                                              D;JNE

                                              """,
                            _ => throw new ArgumentOutOfRangeException()
                        },
                        FunctionCommand functionCommand => functionCommand.Action switch
                        {
                            Action.FUNC => $"""
                                            //function {functionCommand.Name} {functionCommand.Allocation}
                                            ({functionCommand.Name})
                                            @{functionCommand.Allocation}
                                            D=A
                                            @R5 //temp variable for loop
                                            M=D
                                            ({functionCommand.Name}_ALLOC_LCL_LOOP) //Allocate LCL segment
                                            @R5
                                            D=M
                                            @{functionCommand.Name}_ALLOC_LCL_LOOP_DONE
                                            D;JEQ
                                            @SP
                                            D=M
                                            @R5
                                            M=M-1
                                            A=D+M
                                            M=0
                                            @{functionCommand.Name}_ALLOC_LCL_LOOP
                                            0;JMP
                                            ({functionCommand.Name}_ALLOC_LCL_LOOP_DONE)
                                            @{functionCommand.Allocation}
                                            D=A
                                            @SP //Set SP to address after LCL segment
                                            M=D+M

                                            """,
                            Action.CALL => $"""
                                            //call {functionCommand.Name} {functionCommand.Allocation}
                                            @{functionCommand.Name}$ret.{i} //Save return address
                                            D=A
                                            @SP
                                            A=M
                                            M=D
                                            @LCL //Save LCL
                                            D=M
                                            @SP
                                            AM=M+1
                                            M=D
                                            @ARG //Save ARG
                                            D=M
                                            @SP
                                            AM=M+1
                                            M=D
                                            @THIS //Save THIS
                                            D=M
                                            @SP
                                            AM=M+1
                                            M=D
                                            @THAT //Save THAT
                                            D=M
                                            @SP
                                            AM=M+1
                                            M=D
                                            @SP
                                            MD=M+1
                                            @LCL //Set LCL to SP
                                            M=D
                                            @{5 + functionCommand.Allocation}
                                            D=D-A
                                            @ARG //Set ARG to first arg
                                            M=D
                                            @{functionCommand.Name}
                                            0;JMP
                                            ({functionCommand.Name}$ret.{i})
                                            
                                            """,
                            _ => throw new ArgumentOutOfRangeException()
                        },
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    await streamWriter.WriteLineAsync(asmCode);
                }
            }
        }

        public async Task Translate(string vmPath)
        {
            string outputFile, outputFileName;
            string[] vmFiles;
            var hasSys = false;
            if (File.Exists(vmPath))
            {
                outputFileName = GetFileNameWithoutExtension(vmPath);
                var parentFolder = GetDirectoryName(vmPath);
                outputFile = Join(parentFolder, outputFileName) + ".asm";
                vmFiles = [vmPath];
            }
            else if (Directory.Exists(vmPath))
            {
                outputFileName = GetFileName(vmPath);
                outputFile = Join(vmPath, outputFileName) + ".asm";
                vmFiles = Directory.GetFiles(vmPath).Where(f => GetExtension(f).Equals(".vm", StringComparison.OrdinalIgnoreCase)).ToArray();
                hasSys = vmFiles.Any(file => GetFileNameWithoutExtension(file).Equals("sys", StringComparison.OrdinalIgnoreCase));
            }
            else throw new ArgumentException($"Path invalid: {vmPath}");

            var commands = await Parse(vmFiles);
            await Write(commands, outputFile, hasSys);
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
            { "not", Action.NOT },
            { "return", Action.RET },
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
        NOT,
        LABEL,
        GOTO,
        IFGOTO,
        FUNC,
        CALL,
        RET
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
    public interface ICommand
    {
        public Action Action { get; set; }
    }
    public class BasicCommand : ICommand
    {
        public Action Action { get; set; }
    }
    public class PushPopCommand : ICommand
    {
        public Action Action { get; set; }
        public Segment Segment { get; set; }
        public required SegmentProps SegmentProps { get; set; }
        public required short Operand { get; set; }
    }
    public class LabelCommand : ICommand
    {
        public Action Action { get; set; }
        public required string Name { get; set; }
    }
    public class FunctionCommand : ICommand
    {
        public Action Action { get; set; }
        public required string Name { get; set; }
        public required int Allocation { get; set; }
    }
    public record SegmentProps(string Name, string? Address);
}