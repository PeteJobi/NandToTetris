using Assembler;
using VMTranslator;

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
    }
}
