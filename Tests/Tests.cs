using Assembler;
using Compiler;
using VMTranslator;
using static System.IO.Path;

namespace Tests
{
    public class UnitTests
    {
        [Fact]
        public async Task AssemblerTests()
        {
            var assembler = new HackAssembler();
            var result = await assembler.Assemble(@"TestData\Assembler\Add.asm");
            Assert.True(result.IsSuccessful);
            result = await assembler.Assemble(@"TestData\Assembler\Max.asm");
            Assert.True(result.IsSuccessful);
            result = await assembler.Assemble(@"TestData\Assembler\Pong.asm");
            Assert.True(result.IsSuccessful);
        }

        [Fact]
        public async Task TranslatorTests()
        {
            var translator = new HackTranslator();
            await translator.Translate(@"TestData\Translator\SimpleAdd.vm");
            await translator.Translate(@"TestData\Translator\StackTest.vm");
            await translator.Translate(@"TestData\Translator\BasicTest.vm");
            await translator.Translate(@"TestData\Translator\PointerTest.vm");
            await translator.Translate(@"TestData\Translator\StaticTest.vm");
            await translator.Translate(@"TestData\Translator\BasicLoop.vm");
            await translator.Translate(@"TestData\Translator\FibonacciSeries.vm");
            await translator.Translate(@"TestData\Translator\SimpleFunction.vm");
            await translator.Translate(@"TestData\Translator\NestedCall");
            await translator.Translate(@"TestData\Translator\FibonacciElement");
            await translator.Translate(@"TestData\Translator\StaticsTest");
        }

        [Fact]
        public async Task AnalyzerTests()
        {
            var compiler = new JackCompiler();

            await compiler.Compile(@"TestData\Analyzer\ArrayTest");
            await CompareFiles(@"TestData\Analyzer\ArrayTest\Main.jack");

            await compiler.Compile(@"TestData\Analyzer\Square");
            await CompareFiles(@"TestData\Analyzer\Square\Main.jack");
            await CompareFiles(@"TestData\Analyzer\Square\Square.jack");
            await CompareFiles(@"TestData\Analyzer\Square\SquareGame.jack");
            return;

            async Task CompareFiles(string jackPath)
            {
                var parentFolder = GetDirectoryName(jackPath)!;
                var outputFileName = GetFileNameWithoutExtension(jackPath);
                var tokenizerExpectedFile = Join(parentFolder, outputFileName) + "TExpected.xml";
                var analyzerExpectedFile = Join(parentFolder, outputFileName) + "Expected.xml";
                var tokenizerOutputFile = Join(parentFolder, outputFileName) + "T.xml";
                var analyzerOutputFile = Join(parentFolder, outputFileName) + ".xml";

                var expected = await File.ReadAllTextAsync(tokenizerExpectedFile);
                var actual = await File.ReadAllTextAsync(tokenizerOutputFile);
                Assert.Equal(expected, actual);
                expected = await File.ReadAllTextAsync(analyzerExpectedFile);
                actual = await File.ReadAllTextAsync(analyzerOutputFile);
                Assert.Equal(expected, actual);
            }
        }
    }
}
