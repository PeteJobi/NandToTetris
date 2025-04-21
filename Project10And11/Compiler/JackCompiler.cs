using static System.IO.Path;

namespace Compiler
{
    public class JackCompiler
    {
        public async Task Compile(string jackPath)
        {
            string parentFolder;
            string[] jackFiles;
            if (File.Exists(jackPath))
            {
                jackFiles = [jackPath];
                parentFolder = GetDirectoryName(jackFiles[0])!;
            }
            else if (Directory.Exists(jackPath))
            {
                jackFiles = Directory.GetFiles(jackPath)
                    .Where(f => GetExtension(f).Equals(".jack", StringComparison.OrdinalIgnoreCase)).ToArray();
                parentFolder = jackPath;
            }
            else throw new ArgumentException($"Path invalid: {jackPath}");

            foreach (var jackFile in jackFiles)
            {
                var outputFileName = GetFileNameWithoutExtension(jackFile);
                var tokenizerOutputFile = Join(parentFolder, outputFileName) + "T.xml";
                var analyzerOutputFile = Join(parentFolder, outputFileName) + ".xml";
                var tokenList = await new Tokenizer().CreateTokens(jackFile, tokenizerOutputFile);
                await new Analyzer().Analyze(tokenList, analyzerOutputFile);
            }
        }
    }
}
