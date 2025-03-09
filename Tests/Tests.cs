using Assembler;

namespace Tests
{
    public class UnitTests
    {
        [Fact]
        public async Task AssemblerTests()
        {
            var result = await new HackAssembler().Assemble(@"TestData\Add.asm");
            Assert.True(result.IsSuccessful);
            result = await new HackAssembler().Assemble(@"TestData\Max.asm");
            Assert.True(result.IsSuccessful);
            result = await new HackAssembler().Assemble(@"TestData\Pong.asm");
            Assert.True(result.IsSuccessful);
        }
    }
}
