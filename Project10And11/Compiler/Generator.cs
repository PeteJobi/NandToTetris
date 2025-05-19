using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Compiler.Analyzer;

namespace Compiler
{
    public class Generator
    {
        private const string LOCAL = "local";
        private const string ARGUMENT = "argument";
        private const string CONSTANT = "constant";
        private const string POINTER = "pointer";
        private const string THIS = "this";
        private const string THAT = "that";

        public async Task GenerateCode(Tree classTree, string outputFile)
        {
            if (classTree.Name != "class") throw new InvalidOperationException("This structure is not a class.");
            var lines = CompileClass(classTree);

            using (var streamWriter = File.CreateText(outputFile))
            {
                foreach (var line in lines)
                {
                    await streamWriter.WriteLineAsync(line);
                }
            }
        }

        private IEnumerable<string> CompileClass(Tree classStructure)
        {
            var context = new TheClass { ClassName = ((Identifier)classStructure.Contents[1]).Value };
            PopulateClassSymbols((Tree)classStructure.Contents[3], context);
            var subroutineStructures = ((Tree)classStructure.Contents[4]).Contents;
            var lines = subroutineStructures.SelectMany(s => CompileSubroutines((Tree)s, context)).ToArray();
            return lines;
        }

        private void PopulateClassSymbols(Tree classVarDecs, TheClass classContext)
        {
            foreach (Tree decStructure in classVarDecs.Contents)
            {
                var list = ((Keyword)decStructure.Contents[0]).Content == "static" ? classContext.Statics : classContext.Fields;
                var (typeValue, typeIsClass) = GetType(decStructure.Contents[1]);
                list.Add(new SymbolTableObject{ Name = ((Identifier)decStructure.Contents[2]).Value, Type = typeValue, TypeIsClassName = typeIsClass });
                foreach (var extraDecs in ((Tree)decStructure.Contents[3]).Contents)
                {
                    list.Add(new SymbolTableObject{ Name = ((Identifier)((Tree)extraDecs).Contents[1]).Value, Type = typeValue, TypeIsClassName = typeIsClass });
                }
            }
        }

        private IEnumerable<string> CompileSubroutines(Tree subroutine, TheClass classContext)
        {
            var (typeValue, typeIsClass) = GetType(subroutine.Contents[1]);
            var context = new Subroutine
            {
                Type = ((Keyword)subroutine.Contents[0]).Content, ReturnType = typeValue,
                ReturnTypeIsClass = typeIsClass, Class = classContext, SubroutineName = ((Identifier)subroutine.Contents[2]).Value
            };
            if(context.Type == "method") context.Arguments.Add(new SymbolTableObject{ Name = "this", Type = classContext.ClassName, TypeIsClassName = true });
            PopulateSubroutineArguments((Tree)subroutine.Contents[4], context);
            PopulateSubroutineLocals((Tree)((Tree)subroutine.Contents[6]).Contents[1], context);
            var lines = new List<string> { $"function {classContext.ClassName}.{context.SubroutineName} {context.Locals.Count}" };
            if (context.Type == "constructor")
            {
                lines.AddRange([
                    "//Allocate memory for object",
                    $"push {CONSTANT} {classContext.Fields.Count}",
                    "call Memory.alloc 1",
                    $"pop {POINTER} 0",
                    string.Empty
                ]);
            }else if (context.Type == "method")
            {
                lines.AddRange([
                    "//Set first argument to THIS",
                    $"push {ARGUMENT} 0",
                    $"pop {POINTER} 0",
                    string.Empty
                ]);
            }
            lines.AddRange(CompileStatements((Tree)((Tree)subroutine.Contents[6]).Contents[2], context));
            lines.AddRange([
                $"//Subroutine {classContext.ClassName}.{context.SubroutineName} ended",
                string.Empty
            ]);
            return lines;
        }

        private void PopulateSubroutineArguments(Tree parameterList, Subroutine subroutineContext)
        {
            var innerTree = (Tree)parameterList.Contents[0];
            if(!innerTree.Contents.Any()) return;
            innerTree = (Tree)innerTree.Contents[0];
            var (typeValue, typeIsClass) = GetType(innerTree.Contents[0]);
            subroutineContext.Arguments.Add(new SymbolTableObject{ Name = ((Identifier)innerTree.Contents[1]).Value, Type = typeValue, TypeIsClassName = typeIsClass });
            foreach (Tree decStructure in ((Tree)innerTree.Contents[2]).Contents)
            {
                (typeValue, typeIsClass) = GetType(decStructure.Contents[1]);
                subroutineContext.Arguments.Add(new SymbolTableObject{ Name = ((Identifier)decStructure.Contents[2]).Value, Type = typeValue, TypeIsClassName = typeIsClass });
            }
        }

        private void PopulateSubroutineLocals(Tree varDecs, Subroutine subroutineContext)
        {
            foreach (Tree decStructure in varDecs.Contents)
            {
                var (typeValue, typeIsClass) = GetType(decStructure.Contents[1]);
                subroutineContext.Locals.Add(new SymbolTableObject { Name = ((Identifier)decStructure.Contents[2]).Value, Type = typeValue, TypeIsClassName = typeIsClass });
                foreach (Tree extraDecs in ((Tree)decStructure.Contents[3]).Contents)
                {
                    subroutineContext.Locals.Add(new SymbolTableObject { Name = ((Identifier)extraDecs.Contents[1]).Value, Type = typeValue, TypeIsClassName = typeIsClass });
                }
            }
        }

        private IEnumerable<string> CompileStatements(Tree allStatements, Subroutine subroutineContext)
        {
            var innerTree = (Tree)allStatements.Contents[0];
            var lines = new List<string>();

            foreach (Tree statementTree in innerTree.Contents)
            {
                lines.AddRange(statementTree.Name switch
                {
                    "letStatement" => CompileLetStatement(statementTree, subroutineContext),
                    "ifStatement" => CompileIfStatement(statementTree, subroutineContext),
                    "whileStatement" => CompileWhileStatement(statementTree, subroutineContext),
                    "doStatement" => CompileDoStatement(statementTree, subroutineContext),
                    "returnStatement" => CompileReturnStatement(statementTree, subroutineContext),
                    _ => throw new ArgumentOutOfRangeException()
                });
            }

            return lines;
        }

        private IEnumerable<string> CompileLetStatement(Tree letStatement, Subroutine subroutineContext)
        {
            var lines =  CompileExpression((Tree)letStatement.Contents[4], subroutineContext).ToList();
            var identifierName = ((Identifier)letStatement.Contents[1]).Value;
            var symbol = GetSymbol(identifierName, subroutineContext);
            var arrIndexTree = (Tree)letStatement.Contents[2];
            if (arrIndexTree.Contents.Any()) //symbol has array braces
            {
                lines.AddRange(CompileArrayIndex(identifierName, (Tree)((Tree)arrIndexTree.Contents[0]).Contents[1], subroutineContext));
                lines.AddRange([
                    "//Set array item value",
                    $"pop {THAT} 0"
                ]);
            }
            else
            {
                lines.Add($"pop {symbol.Segment} {symbol.Index}");
            }
            return lines;
        }

        private IEnumerable<string> CompileIfStatement(Tree ifStatement, Subroutine subroutineContext)
        {
            var lines = CompileExpression((Tree)ifStatement.Contents[2], subroutineContext).ToList();
            bool hasElse = ((Tree)ifStatement.Contents[7]).Contents.Any();
            var currentLabel =
                $"{subroutineContext.Class.ClassName}.{subroutineContext.SubroutineName}_{++subroutineContext.LabelCount}_";
            lines.AddRange([
                "not",
                $"if-goto {currentLabel}if_end",
                "//Statements for True block"
            ]);
            lines.AddRange(CompileStatements((Tree)ifStatement.Contents[5], subroutineContext));
            if(hasElse) lines.Add($"goto {currentLabel}else_end");
            lines.Add($"label {currentLabel}if_end");
            if (hasElse)
            {
                lines.Add("//Statements for False block");
                lines.AddRange(CompileStatements((Tree)((Tree)((Tree)ifStatement.Contents[7]).Contents[0]).Contents[2], subroutineContext));
                lines.Add($"label {currentLabel}else_end");
            }
            return lines;
        }

        private IEnumerable<string> CompileWhileStatement(Tree whileStatement, Subroutine subroutineContext)
        {
            var currentLabel =
                $"{subroutineContext.Class.ClassName}.{subroutineContext.SubroutineName}_{++subroutineContext.LabelCount}_";
            var lines = new List<string>{$"label {currentLabel}while_start"};
            lines.AddRange(CompileExpression((Tree)whileStatement.Contents[2], subroutineContext).ToList());
            lines.AddRange([
                "not",
                $"if-goto {currentLabel}while_end",
                "//Statements for While block"
            ]);
            lines.AddRange(CompileStatements((Tree)whileStatement.Contents[5], subroutineContext));
            lines.AddRange([
                $"goto {currentLabel}while_start",
                $"label {currentLabel}while_end"
            ]);
            return lines;
        }

        private IEnumerable<string> CompileDoStatement(Tree doStatement, Subroutine subroutineContext)
        {
            var lines = CompileSubroutineCall((Tree)((Tree)doStatement.Contents[1]).Contents[0], subroutineContext);
            return lines.Append("pop temp 0");
        }

        private IEnumerable<string> CompileReturnStatement(Tree returnStatement, Subroutine subroutineContext)
        {
            var lines = new List<string>();
            var expressionTree = (Tree)returnStatement.Contents[1];
            if (subroutineContext.ReturnType != "void")
            {
                if (expressionTree.Contents.Any()) lines.AddRange(CompileExpression((Tree)expressionTree.Contents[0], subroutineContext));
                else
                    throw new InvalidOperationException(
                        $"Non-void subroutine {subroutineContext.SubroutineName} must return a {subroutineContext.ReturnType} value");
            }else lines.Add($"push {CONSTANT} 0");
            lines.Add("return");
            return lines;
        }

        private (string Segment, int Index, SymbolTableObject Symbol) GetSymbol(string symbolName, Subroutine subroutineContext)
        {
            var segments = new KeyValuePair<string, List<SymbolTableObject>>[]
            {
                new(LOCAL, subroutineContext.Locals),
                new(ARGUMENT, subroutineContext.Arguments),
                new(THIS, subroutineContext.Class.Fields),
                new("static", subroutineContext.Class.Statics),
            };

            foreach (var segment in segments)
            {
                for (var i = 0; i < segment.Value.Count; i++)
                {
                    var symbol = segment.Value[i];
                    if (symbol.Name == symbolName) return (segment.Key, i, symbol);
                }
            }

            throw new ArgumentException($"Symbol {symbolName} not present.");
        }

        private (string Segment, int Index, SymbolTableObject Symbol)? GetSymbolOrNull(string symbolName, Subroutine subroutineContext)
        {
            try
            {
                return GetSymbol(symbolName, subroutineContext);
            }
            catch
            {
                return null;
            }
        }

        private IEnumerable<string> CompileExpression(Tree expression, Subroutine subroutineContext)
        {
            var term = expression.Contents[0];
            var lines = CompileTerm((Tree)term, subroutineContext).ToList();
            var moreTermsTree = (Tree)expression.Contents[1];
            if (!moreTermsTree.Contents.Any()) return lines;
            foreach (Tree opTree in moreTermsTree.Contents)
            {
                lines.AddRange(CompileTerm((Tree)opTree.Contents[1], subroutineContext));
                lines.Add(((Symbol)opTree.Contents[0]).Content switch
                {
                    '+' => "add",
                    '-' => "sub",
                    '*' => "call Math.multiply 2",
                    '/' => "call Math.divide 2",
                    '&' => "and",
                    '|' => "or",
                    '<' => "lt",
                    '>' => "gt",
                    '=' => "eq",
                    _ => throw new ArgumentOutOfRangeException()
                });
            }
            return lines;
        }

        private IEnumerable<string> CompileTerm(Tree term, Subroutine subroutineContext)
        {
            var innerStructure = term.Contents[0];
            return innerStructure switch
            {
                Keyword keyword => CompileKeywordConstant(keyword),
                Constant constant => CompileConstant(constant),
                Identifier identifier => CompileIdentifier(identifier, subroutineContext),
                Tree tree => tree.Contents[0] switch
                {
                    Identifier identifier => CompileIdentifier(identifier, subroutineContext, (Tree)tree.Contents[2]),
                    Symbol symbol => symbol.Content == '(' ? CompileParenthesis(tree, subroutineContext) : CompileUnaryOperation(symbol, tree, subroutineContext),
                    Tree innerTree => CompileSubroutineCall(innerTree, subroutineContext),
                    _ => throw new ArgumentOutOfRangeException()
                }
            };
        }

        private IEnumerable<string> CompileConstant(Constant constant)
        {
            switch (constant.Type)
            {
                case ConstantType.INTEGER:
                    return [$"push {CONSTANT} {constant.Value}"];
                case ConstantType.STRING:
                    var lines = new List<string>();
                    lines.AddRange([
                        $"//Create and push \"{constant.Value}\"",
                        $"push {CONSTANT} {constant.Value.Length}",
                        "call String.new 1"
                    ]);
                    lines.AddRange(constant.Value.SelectMany<char, string>(s => [
                        $"push {CONSTANT} {(int)s}",
                        "call String.appendChar 2"
                    ]));
                    return lines;
                default:
                    throw new ArgumentOutOfRangeException(nameof(constant.Type));
            }
        }

        private IEnumerable<string> CompileKeywordConstant(Keyword keywordConstant)
        {
            return keywordConstant.Content switch
            {
                "true" => [$"push {CONSTANT} 1", "neg"],
                "false" or "null" => [$"push {CONSTANT} 0"],
                "this" => [$"push {POINTER} 0"],
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerable<string> CompileIdentifier(Identifier identifier, Subroutine subroutineContext, Tree? indexExpressionTree = null)
        {
            var (segment, index, _) = GetSymbol(identifier.Value, subroutineContext);
            if (indexExpressionTree != null)
            {
                return CompileArrayIndex(identifier.Value, indexExpressionTree, subroutineContext)
                    .Append($"push {THAT} 0");
            }
            return [$"push {segment} {index}"];
        }

        private IEnumerable<string> CompileParenthesis(Tree parenthesisTree, Subroutine subroutineContext)
        {
            return CompileExpression((Tree)parenthesisTree.Contents[1], subroutineContext);
        }

        private IEnumerable<string> CompileUnaryOperation(Symbol sign, Tree unaryOpTree, Subroutine subroutineContext)
        {
            var lines = CompileTerm((Tree)unaryOpTree.Contents[1], subroutineContext);
            return lines.Append(sign.Content switch
            {
                '-' => "neg",
                '~' => "not",
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        private IEnumerable<string> CompileSubroutineCall(Tree subroutineCall, Subroutine subroutineContext)
        {
            var lines = new List<string>();
            var expressionListTree = (Tree)((Tree)subroutineCall.Contents[^2]).Contents[0];
            var argsCount = 0;
            var firstIdentifier = (Identifier)subroutineCall.Contents[0];
            string subroutineClassName; string subroutineName;
            if (firstIdentifier.Type == IdentifierType.SUBROUTINE_NAME) //This is a method in this class
            {
                argsCount++;
                lines.Add($"push {POINTER} 0"); //Push the current class object as first argument
                subroutineClassName = subroutineContext.Class.ClassName;
                subroutineName = firstIdentifier.Value;
            }
            else
            {
                var secondIdentifier = (Identifier)subroutineCall.Contents[2];
                subroutineName = secondIdentifier.Value;
                var symbol = GetSymbolOrNull(firstIdentifier.Value, subroutineContext);
                if (symbol == null) //This is a function from this class or an external class
                {
                    subroutineClassName = firstIdentifier.Value;
                }
                else //This is a method called via a variable
                {
                    if (!symbol.Value.Symbol.TypeIsClassName)
                        throw new InvalidOperationException($"Symbol {symbol.Value.Symbol.Name} is not a class object");
                    argsCount++;
                    lines.Add($"push {symbol.Value.Segment} {symbol.Value.Index}");
                    subroutineClassName = symbol.Value.Symbol.Type;
                }
            }
            if (expressionListTree.Contents.Any())
            {
                argsCount++;
                expressionListTree = (Tree)expressionListTree.Contents[0];
                lines.AddRange(CompileExpression((Tree)expressionListTree.Contents[0], subroutineContext));
                foreach (Tree extraExps in ((Tree)expressionListTree.Contents[1]).Contents)
                {
                    argsCount++;
                    lines.AddRange(CompileExpression((Tree)extraExps.Contents[1], subroutineContext));
                }
            }
            lines.Add($"call {subroutineClassName}.{subroutineName} {argsCount}");
            return lines;
        }

        private IEnumerable<string> CompileArrayIndex(string identifierName, Tree indexExpressionTree, Subroutine subroutineContext)
        {
            var lines = new List<string>();
            var symbol = GetSymbol(identifierName, subroutineContext);
            if (symbol.Symbol.Type != "Array")
                throw new InvalidOperationException($"Symbol {symbol.Symbol.Name} is not an array");
            lines.AddRange([
                $"push {symbol.Segment} {symbol.Index}",
                "//Compute index expression and add to above value"
            ]);
            lines.AddRange(CompileExpression(indexExpressionTree, subroutineContext));
            lines.AddRange([
                "add",
                $"pop {POINTER} 1"
            ]);
            return lines;
        }

        private (string TypeValue, bool TypeIsClass) GetType(Structure typeStructure)
        {
            var typeIsClass = typeStructure is Identifier;
            var typeValue = typeStructure is Identifier identifier ? identifier.Value :
                typeStructure is Keyword keyword ? keyword.Content : throw new NotImplementedException();
            return (typeValue!, typeIsClass);
        }


        public class TheClass
        {
            public string ClassName { get; set; }
            public List<SymbolTableObject> Fields { get; set; } = [];
            public List<SymbolTableObject> Statics { get; set; } = [];
        }

        public class Subroutine
        {
            public string SubroutineName { get; set; }
            public string Type { get; set; }
            public string ReturnType { get; set; }
            public bool ReturnTypeIsClass { get; set; }
            public int LabelCount { get; set; }
            public List<SymbolTableObject> Arguments { get; set; } = [];
            public List<SymbolTableObject> Locals { get; set; } = [];
            public TheClass Class { get; set; }
        }

        public class SymbolTableObject
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool TypeIsClassName { get; set; }
        }
    }
}
