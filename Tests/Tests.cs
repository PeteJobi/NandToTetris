using Assembler;

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
    }
}
