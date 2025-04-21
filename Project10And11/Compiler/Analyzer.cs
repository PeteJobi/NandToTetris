using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Compiler.Tokenizer;

namespace Compiler
{
    public class Analyzer
    {
        //private List<string> errs = []; //Something I used to analyze errors. Not really part of the expected program
        public Analyzer()
        {
            Statements.Contents = StatementContents;
            Term.Contents = TermContents;
        }

        public async Task Analyze(TokenList tokenList, string outputFile)
        {
            var analysis = ProcessStructure(Class, tokenList, 0);
            if (analysis.Lines == null) throw new Exception(analysis.ErrorMessage);
            if (tokenList.HasMoreTokens()) throw new Exception($"Unexpected token after class: \"{tokenList.Advance().Value}\"");

            using (var streamWriter = File.CreateText(outputFile))
            {
                foreach (var line in analysis.Lines)
                {
                    await streamWriter.WriteLineAsync(line);
                }
            }
        }

        public (IEnumerable<string>? Lines, string? ErrorMessage) ProcessStructure(Structure structure, TokenList tokenList, int depth)
        {
            var indentation = string.Join("", Enumerable.Repeat("  ", depth));
            switch (structure)
            {
                case Identifier:
                    if (!tokenList.HasMoreTokens()) throw new InvalidOperationException($"Unexpected token: {structure}");
                    var token = tokenList.Advance();
                    if (token.Type == TokenType.IDENTIFIER)
                        return ([GetTagWithContent(indentation, "identifier", token.Value)], null);
                    tokenList.BackTrack();
                    return (null, $"Identifier token was expected. Got \"{token.Value}\" instead");

                case Keyword keyword:
                    if (!tokenList.HasMoreTokens()) throw new InvalidOperationException($"Unexpected token: {structure}");
                    token = tokenList.Advance();
                    if (token.Type == TokenType.KEYWORD && token.Value == keyword.Content)
                        return ([GetTagWithContent(indentation, "keyword", keyword.Content)], null);
                    tokenList.BackTrack();
                    return (null,
                        $"Keyword token of value \"{keyword.Content}\" was expected. Got \"{token.Value}\" instead.");

                case Symbol symbol:
                    if (!tokenList.HasMoreTokens()) throw new InvalidOperationException($"Unexpected token: {structure}");
                    token = tokenList.Advance();
                    if (token.Type == TokenType.SYMBOL && token.Value[0] == symbol.Content)
                        return ([GetTagWithContent(indentation, "symbol", symbol.Content.ToString())], null);
                    tokenList.BackTrack();
                    return (null,
                        $"Symbol token of value \"{symbol.Content}\" was expected. Got \"{token.Value}\" instead.");

                case Constant constant:
                    if (!tokenList.HasMoreTokens()) throw new InvalidOperationException($"Unexpected token: {structure}");
                    token = tokenList.Advance();
                    if (constant.Type == ConstantType.INTEGER && token.Type != TokenType.INT_CONST
                        || constant.Type == ConstantType.STRING && token.Type != TokenType.STRING_CONST)
                        return (null, $"Constant of type {constant} was expected. Got \"{token.Value}\" instead.");
                    return ([GetTagWithContent(indentation, $"{constant}Constant", token.Value)], null);

                case Or or:
                    var currentIndex = tokenList.GetCurrentIndex();
                    var exceptionMessages = new List<string>();
                    foreach (var orContent in or.Contents)
                    {
                        var data = ProcessStructure(orContent, tokenList, depth);
                        if (data.Item1 != null)
                        {
                            return (data.Item1, null);
                        }
                        exceptionMessages.Add(data.Item2!);
                        tokenList.SetCurrentIndex(currentIndex);
                    }
                    return (null, 
                        $"Supplied token did not match any of the expected tokens.\n{string.Join("\n", exceptionMessages)}");

                case Vary vary:
                    var varyRes = new List<string>();
                    var i = vary.Quantifier == StructureQuantifier.ZERO_OR_ONE ? 1 : int.MaxValue;
                    while (i > 0)
                    {
                        currentIndex = tokenList.GetCurrentIndex();
                        var data = ProcessStructure(vary.Content, tokenList, depth);
                        if (data.Item1 == null)
                        {
                            tokenList.SetCurrentIndex(currentIndex);
                            break;
                        }
                        varyRes.AddRange(data.Item1);
                        i--;
                    }
                    return (varyRes, null);

                case Tree tree:
                    var treeRes = new List<string>();
                    foreach (var treeContent in tree.Contents)
                    {
                        var data = ProcessStructure(treeContent, tokenList, depth + (tree.Name == null ? 0 : 1));
                        if (data.Item1 == null)
                        {
                            //errs.Add($"{depth} {tree.Name}-{treeContent.Name ?? treeContent.ToString()}  {data.Item2!}");
                            return (null, data.Item2);
                        }
                        //errs.Add($"{depth} {tree.Name}-{treeContent.Name ?? treeContent.ToString()} {data.Item1.Count()}  FIXED");
                        //if(depth == 0 && data.Item1.Count() == 32) errs.Add(string.Join("\n", data.Item1));
                        treeRes.AddRange(data.Item1);
                    }
                    if (tree.Name != null)
                    {
                        treeRes.Insert(0, $"{indentation}<{tree.Name}>");
                        treeRes.Add($"{indentation}</{tree.Name}>");
                    }
                    return (treeRes, null);
                default:
                    return (null, $"Structure \"{structure.GetType()}\" not supported");
            }
        }

        private string GetTagWithContent(string indentation, string tagName, string content) =>
            $"{indentation}<{tagName}> {content.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")} </{tagName}>";

        private static readonly Or Type = new()
        {
            Contents = [new Keyword("int"), new Keyword("char"), new Keyword("boolean"), new Identifier()]
        };
        private static readonly Vary CommaVarNameMultiple = new (StructureQuantifier.ZERO_OR_MORE, new Tree
        {
            Contents =
            [
                new Symbol(','),
                new Identifier()
            ]
        });
        private static readonly Tree Term = new() { Name = "term", Contents = [] };
        private static readonly Tree Expression = new()
        {
            Name = "expression",
            Contents = [
                Term,
                new Vary(StructureQuantifier.ZERO_OR_MORE, new Tree
                {
                    Contents = [
                        new Or
                        {
                            Contents = [
                                new Symbol('+'),
                                new Symbol('-'),
                                new Symbol('*'),
                                new Symbol('/'),
                                new Symbol('&'),
                                new Symbol('|'),
                                new Symbol('<'),
                                new Symbol('>'),
                                new Symbol('='),
                            ]
                        },
                        Term
                    ]
                })
            ]
        };
        private static readonly Tree ExpressionList = new()
        {
            Name = "expressionList",
            Contents = [
                new Vary(StructureQuantifier.ZERO_OR_ONE, new Tree
                {
                    Contents =
                    [
                        Expression,
                        new Vary(StructureQuantifier.ZERO_OR_MORE, new Tree
                        {
                            Contents =
                            [
                                new Symbol(','),
                                Expression
                            ]
                        })
                    ]
                })
            ]
        };
        private static readonly Tree SubroutineCall = new()
        {
            Contents =
            [
                new Or
                {
                    Contents =
                    [
                        new Tree
                        {
                            Contents =
                            [
                                new Identifier(),
                                new Symbol('('),
                                ExpressionList,
                                new Symbol(')')
                            ]
                        },
                        new Tree
                        {
                            Contents =
                            [
                                new Or { Contents = [new Identifier(), new Identifier()] },
                                new Symbol('.'),
                                new Identifier(),
                                new Symbol('('),
                                ExpressionList,
                                new Symbol(')')
                            ]
                        }
                    ]
                }
            ]
        };
        private static readonly Structure[] TermContents = [
            new Or
            {
                Contents = [
                    SubroutineCall,
                    new Keyword("true"),
                    new Keyword("false"),
                    new Keyword("null"),
                    new Keyword("this"),
                    new Tree
                    {
                        Contents = [
                            new Identifier(),
                            new Symbol('['),
                            Expression,
                            new Symbol(']')
                        ]
                    },
                    new Identifier(),
                    new Tree
                    {
                        Contents = [
                            new Symbol('('),
                            Expression,
                            new Symbol(')')
                        ]
                    },
                    new Tree
                    {
                        Contents = [
                            new Or{ Contents = [new Symbol('-'), new Symbol('~')] },
                            Term
                        ]
                    },
                    new Constant(ConstantType.INTEGER),
                    new Constant(ConstantType.STRING)
                ]
            }
        ];
        private static readonly Tree Statements = new(){ Name = "statements", Contents = [] };
        private static readonly Structure[] StatementContents = [new Vary(StructureQuantifier.ZERO_OR_MORE, new Or
        {
            Contents =
            [
                new Tree
                {
                    Name = "letStatement",
                    Contents =
                    [
                        new Keyword("let"),
                        new Identifier(),
                        new Vary(StructureQuantifier.ZERO_OR_ONE, new Tree
                        {
                            Contents =
                            [
                                new Symbol('['),
                                Expression,
                                new Symbol(']')
                            ]
                        }),
                        new Symbol('='),
                        Expression,
                        new Symbol(';')
                    ]
                },
                new Tree
                {
                    Name = "ifStatement",
                    Contents =
                    [
                        new Keyword("if"),
                        new Symbol('('),
                        Expression,
                        new Symbol(')'),
                        new Symbol('{'),
                        Statements,
                        new Symbol('}'),
                        new Vary(StructureQuantifier.ZERO_OR_ONE, new Tree
                        {
                            Contents = [
                                new Keyword("else"),
                                new Symbol('{'),
                                Statements,
                                new Symbol('}')
                            ]
                        })
                    ]
                },
                new Tree
                {
                    Name = "whileStatement",
                    Contents = [
                        new Keyword("while"),
                        new Symbol('('),
                        Expression,
                        new Symbol(')'),
                        new Symbol('{'),
                        Statements,
                        new Symbol('}'),
                    ]
                },
                new Tree
                {
                    Name = "doStatement",
                    Contents = [
                        new Keyword("do"),
                        SubroutineCall,
                        new Symbol(';')
                    ]
                },
                new Tree
                {
                    Name = "returnStatement",
                    Contents = [
                        new Keyword("return"),
                        new Vary(StructureQuantifier.ZERO_OR_ONE, Expression),
                        new Symbol(';')
                    ]
                }
            ]
        })];
        private static readonly Tree Class = new()
        {
            Name = "class",
            Contents =
            [
                new Keyword("class"),
                new Identifier(),
                new Symbol('{'),
                new Vary(StructureQuantifier.ZERO_OR_MORE, new Tree
                {
                    Name = "classVarDec",
                    Contents =
                    [
                        new Or { Contents = [new Keyword("static"), new Keyword("field")] },
                        Type,
                        new Identifier(),
                        CommaVarNameMultiple,
                        new Symbol(';')
                    ]
                }),
                new Vary(StructureQuantifier.ZERO_OR_MORE, new Tree
                {
                    Name = "subroutineDec",
                    Contents = [
                        new Or{ Contents = [new Keyword("constructor"), new Keyword("function"), new Keyword("method")] },
                        new Or{ Contents = [new Keyword("void"), Type] },
                        new Identifier(),
                        new Symbol('('),
                        new Tree
                        {
                            Name = "parameterList",
                            Contents = [
                                new Vary(StructureQuantifier.ZERO_OR_ONE, new Tree
                                {
                                    Contents = [
                                        Type,
                                        new Identifier(),
                                        new Vary(StructureQuantifier.ZERO_OR_MORE, new Tree
                                        {
                                            Contents = [
                                                new Symbol(','),
                                                Type,
                                                new Identifier()
                                            ]
                                        })
                                    ]
                                })
                            ]
                        },
                        new Symbol(')'),
                        new Tree
                        {
                            Name = "subroutineBody",
                            Contents = [
                                new Symbol('{'),
                                new Vary(StructureQuantifier.ZERO_OR_MORE, new Tree
                                {
                                    Name = "varDec",
                                    Contents = [
                                        new Keyword("var"),
                                        Type,
                                        new Identifier(),
                                        CommaVarNameMultiple,
                                        new Symbol(';')
                                    ]
                                }),
                                Statements,
                                new Symbol('}')
                            ]
                        }
                    ]
                }),
                new Symbol('}'),
            ]
        };
        public enum StructureQuantifier{ ZERO_OR_ONE, ZERO_OR_MORE }
        public enum ConstantType{ STRING, INTEGER }
        public class Structure
        {
            public string? Name { get; set; }
        }
        public class Or: Structure
        {
            public required Structure[] Contents { get; set; }
            public override string ToString() => string.Join<Structure>(" or ", Contents);
        }
        public class Vary(StructureQuantifier quantifier, Structure content) : Structure
        {
            public Structure Content { get; init; } = content;
            public StructureQuantifier Quantifier { get; init; } = quantifier;
            public override string ToString() => $"{(Quantifier == StructureQuantifier.ZERO_OR_ONE ? "?" : "*")}" +
                                                 $"({Content.ToString() ?? throw new InvalidOperationException()})";
        }
        public class Tree: Structure
        {
            public required Structure[] Contents { get; set; }
            public override string ToString() => Name switch
            {
                "statements" => "statements",
                "term" => "term",
                _ => string.Join<Structure>(", ", Contents)
            };
        }
        public class Keyword(string content) : Structure
        {
            public string Content { get; set; } = content;
            public override string ToString() => Content;
        }
        public class Symbol(char content) : Structure
        {
            public char Content { get; set; } = content;
            public override string ToString() => Content.ToString();
        }
        public class Identifier : Structure
        {
            public override string ToString() => "identifier";
        }
        public class Constant(ConstantType type) : Structure
        {
            public ConstantType Type { get; set; } = type;
            public override string ToString() => Type switch
            {
                ConstantType.INTEGER => "integer",
                ConstantType.STRING => "string",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
