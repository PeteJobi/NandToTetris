using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class Tokenizer
    {
        public async Task<TokenList> CreateTokens(string jackPath, string? outputFile = null)
        {
            var tokens = new List<Token>();
            var builder = new StringBuilder();
            using (var streamReader = File.OpenText(jackPath))
            {
                var insideComment = false;
                while (await streamReader.ReadLineAsync() is { } line)
                {
                    line = line.TrimStart();
                    if (line == string.Empty || line.StartsWith("//")) continue;
                    var insideQuote = false;
                    for (var i = 0; i < line.Length; i++)
                    {
                        var ch = line[i];
                        if (!insideQuote && !insideComment && char.IsWhiteSpace(ch))
                        {
                            AddBuilderToTokens();
                            continue;
                        }

                        if (!insideComment && ch == '"')
                        {
                            if (!insideQuote)
                            {
                                AddBuilderToTokens();
                                insideQuote = true;
                                continue;
                            }

                            tokens.Add(new Token { Value = builder.ToString(), Type = TokenType.STRING_CONST });
                            builder.Clear();
                            insideQuote = false;
                            continue;
                        }

                        if (!insideQuote && !insideComment && ch == '/')
                        {
                            if (i < line.Length - 1 && line[i + 1] == '/')
                            {
                                AddBuilderToTokens();
                                break;
                            }
                            if (i < line.Length - 2 && line[(i + 1)..(i + 3)] == "**")
                            {
                                AddBuilderToTokens();
                                insideComment = true;
                                i += 2;
                                continue;
                            }
                        }
                        if (!insideQuote && insideComment && ch == '*' && i < line.Length - 1 && line[i + 1] == '/')
                        {
                            insideComment = false;
                            i++;
                            continue;
                        }

                        if (insideComment) continue;

                        if (!insideQuote && _symbols.Contains(ch))
                        {
                            AddBuilderToTokens();
                            tokens.Add(new Token { Value = ch.ToString(), Type = TokenType.SYMBOL });
                        }
                        else builder.Append(ch);
                    }
                    if (insideQuote) throw new InvalidOperationException($"String literal cannot extend to next line ({builder})");
                    if (!insideComment) AddBuilderToTokens();
                }
            }

            if(outputFile != null) await WriteToFile();
            return new TokenList(tokens);

            void AddBuilderToTokens()
            {
                if (builder.Length == 0) return;
                var text = builder.ToString();
                if(!AddIdentifierTokenOrNot(text, tokens)) throw new InvalidOperationException($"{text} is not recognized");
                builder.Clear();
            }

            async Task WriteToFile()
            {
                await using var streamWriter = File.CreateText(outputFile);
                await streamWriter.WriteLineAsync("<tokens>");
                foreach (var token in tokens)
                {
                    var tag = token.Type switch
                    {
                        TokenType.KEYWORD => "keyword",
                        TokenType.SYMBOL => "symbol",
                        TokenType.IDENTIFIER => "identifier",
                        TokenType.INT_CONST => "integerConstant",
                        TokenType.STRING_CONST => "stringConstant",
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    await streamWriter.WriteLineAsync(GetTagWithContent(tag, token.Value));
                }
                await streamWriter.WriteLineAsync("</tokens>");
            }

            string GetTagWithContent(string tagName, string content) => 
                $"<{tagName}> {content.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")} </{tagName}>";
        }

        private TokenType? GetTokenType(string tokenValue)
        {
            if (_keywords.Contains(tokenValue)) return TokenType.KEYWORD;
            if (tokenValue.Length == 1 && _symbols.Contains(tokenValue[0])) return TokenType.SYMBOL;
            if (short.TryParse(tokenValue, out var num) && num >= 0) return TokenType.INT_CONST;
            return null;
        }

        private bool AddTokenOrNot(string tokenValue, List<Token> list)
        {
            var tokenType = GetTokenType(tokenValue);
            if (tokenType == null) return false;
            list.Add(new Token { Value = tokenValue, Type = tokenType.Value });
            return true;
        }

        private bool AddIdentifierTokenOrNot(string tokenValue, List<Token> list)
        {
            if (AddTokenOrNot(tokenValue, list)) return true;
            var firstChar = tokenValue[0];
            if (firstChar != '_' && !IsLetter(firstChar)) return false; //If token does not start with an underscore or letter, it can't be an identifier
            if (tokenValue[1..].Any(ch => ch != '_' && !IsLetter(ch) && !IsDigit(ch))) return false; //If the remaining chars of the token is not an underscore, letter or digit, it's not an identifier
            list.Add(new Token { Value = tokenValue, Type = TokenType.IDENTIFIER });
            return true;
        }

        private static bool IsDigit(char c) => c is >= '0' and <= '9';
        private static bool IsLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

        private readonly HashSet<string> _keywords =
        [
            "class", "field", "static", "var", "true", "false", "null", "this", "let", "do", "if", "else", "while", "return", "constructor", "function", "method", "int", "char", "boolean", "void"
        ];

        private readonly HashSet<char> _symbols =
        [
            '(', ')', '{', '}', '[', ']', '.', ',', ':', ';', '+', '-', '*', '/', '&', '|', '<', '>', '=', '~'
        ];

        public enum TokenType
        {
            KEYWORD, SYMBOL, IDENTIFIER, INT_CONST, STRING_CONST
        }

        public class Token
        {
            public string Value { get; set; }
            public TokenType Type { get; set; }
            public override string ToString() => $"{Value} ({Type})";
        }

        public class TokenList(List<Token> list)
        {
            private int _currentIndex = -1;

            public Token Advance() => list[++_currentIndex];
            public void BackTrack() => _currentIndex--;

            public bool HasMoreTokens() => _currentIndex < list.Count - 1;
            public int GetCurrentIndex() => _currentIndex;
            public void SetCurrentIndex(int index) => _currentIndex = index;
        }
    }
}
