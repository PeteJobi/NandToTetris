using static System.IO.Path;

namespace Compiler
{
    public class JackCompiler
    {
        public async Task Analyze(string jackPath)
        {
            var (jackFiles, parentFolder) = GetJackFiles(jackPath);

            foreach (var jackFile in jackFiles)
            {
                var outputFileName = GetFileNameWithoutExtension(jackFile);
                var tokenizerOutputFile = Join(parentFolder, outputFileName) + "T.xml";
                var tokenList = await new Tokenizer().CreateTokens(jackFile, tokenizerOutputFile);
                var analyzerOutputFile = Join(parentFolder, outputFileName) + ".xml";
                await new Analyzer().Analyze(tokenList, analyzerOutputFile);
            }
        }

        public async Task Compile(string jackPath)
        {
            var (jackFiles, parentFolder) = GetJackFiles(jackPath);

            foreach (var jackFile in jackFiles)
            {
                var outputFileName = GetFileNameWithoutExtension(jackFile);
                var tokenList = await new Tokenizer().CreateTokens(jackFile);
                var structure = new Analyzer().Structurize(tokenList);
                var generatorOutputFile = Join(parentFolder, outputFileName) + ".vm";
                if (structure is Analyzer.Tree { Name: "class" } tree)
                    await new Generator().GenerateCode(tree, generatorOutputFile);
                else throw new InvalidOperationException("Code supplied is not a valid class");
            }
        }

        private (IEnumerable<string> JackFiles, string ParentFolder) GetJackFiles(string jackPath)
        {
            if (File.Exists(jackPath))
                return ([jackPath], GetDirectoryName(jackPath)!);

            if (!Directory.Exists(jackPath)) throw new ArgumentException($"Path invalid: {jackPath}");

            var jackFiles = Directory.GetFiles(jackPath)
                .Where(f => GetExtension(f).Equals(".jack", StringComparison.OrdinalIgnoreCase)).ToArray();
            return (jackFiles, jackPath);
        }
    }
}
