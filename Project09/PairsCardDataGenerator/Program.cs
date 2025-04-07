const int width = 48;
const int height = 54;
const int maxAddress = 32767;
await Generate(new[]
{
    "Back",
    "Ball",
    "Book",
    "Chest",
    "Coins",
    "Flame",
    "Flower",
    "Ghost",
    "Knight",
    "Night",
    "Panda",
    "Potion",
    "Skull",
    "Smiley",
    "Strawberry",
    "SwordAndShield",
    "Wizard",
}.Select(p => $"CardsBinaryData\\{p}.txt").ToArray());
return;

async Task Generate(string[] cardBinaryDataPaths)
{
    await using var streamWriter = File.CreateText("CardsData.jack");
    await streamWriter.WriteLineAsync("class CardsData {");

    await streamWriter.WriteLineAsync("\tfunction Array getFlipAnimationTopBottomData(){");
    await streamWriter.WriteLineAsync("\t\tvar Array data;");
    var flipAnimTopBottomData = FlipAnimationTopBottomData().ToArray();
    await streamWriter.WriteLineAsync($"\t\tlet data = Array.new({flipAnimTopBottomData.Length + 1});");
    await streamWriter.WriteLineAsync($"\t\tlet data[0] = {flipAnimTopBottomData.Length};");
    for (var i = 0; i < flipAnimTopBottomData.Length; i++)
    {
        await streamWriter.WriteLineAsync($"\t\tlet data[{i + 1}] = {flipAnimTopBottomData[i]};");
    }
    await streamWriter.WriteLineAsync("\t\treturn data;");
    await streamWriter.WriteLineAsync("\t}");

    await streamWriter.WriteLineAsync("\tfunction Array getFlipAnimationSidesData(){");
    await streamWriter.WriteLineAsync("\t\tvar Array data;");
    var flipAnimSidesData = FlipAnimationSidesData().ToArray();
    await streamWriter.WriteLineAsync($"\t\tlet data = Array.new({flipAnimSidesData.Length + 1});");
    await streamWriter.WriteLineAsync($"\t\tlet data[0] = {flipAnimSidesData.Length};");
    for (var i = 0; i < flipAnimSidesData.Length; i++)
    {
        await streamWriter.WriteLineAsync($"\t\tlet data[{i + 1}] = {flipAnimSidesData[i]};");
    }
    await streamWriter.WriteLineAsync("\t\treturn data;");
    await streamWriter.WriteLineAsync("\t}");


    foreach (var binaryPath in cardBinaryDataPaths)
    {
        var cardName = Path.GetFileNameWithoutExtension(binaryPath);
        await streamWriter.WriteLineAsync();
        await streamWriter.WriteLineAsync($"\tfunction Array get{cardName}Card(){{");
        await streamWriter.WriteLineAsync("\t\tvar Array data;");
        await streamWriter.WriteLineAsync("\t\tlet data = Array.new(4 * 54);");
        using (var streamReader = File.OpenText(binaryPath))
        {
            for (var i = 0; i < height; i++)
            {
                var line = await streamReader.ReadLineAsync() ?? string.Empty;
                var shorts = LineToShorts(PadOrTrimLine(line));
                for (var j = 0; j < shorts.Length; j++)
                {
                    var ind = i * 4 + j;
                    var value = shorts[j].ToString();
                    if (shorts[j] < -maxAddress)
                    {
                        value = $"0-{maxAddress}{shorts[j] + maxAddress}";
                    }
                    await streamWriter.WriteLineAsync($"\t\tlet data[{ind}] = {value};");
                }
            }
            await streamWriter.WriteLineAsync("\t\treturn data;");
        }
        await streamWriter.WriteLineAsync("\t}");
    }
    await streamWriter.WriteLineAsync("}");
}

string PadOrTrimLine(string line)
{
    switch (line.Length)
    {
        case > width:
        {
            var excess = (line.Length - width) / (double)2;
            return line[(int)Math.Floor(excess)..(width + (int)Math.Ceiling(excess))];
        }
        case width:
            return line;
    }

    var remainder = (width - line.Length) / (double)2;
    var leftPadding = new string(Enumerable.Repeat('0', (int)Math.Floor(remainder)).ToArray());
    var rightPadding = new string(Enumerable.Repeat('0', (int)Math.Ceiling(remainder)).ToArray());
    return leftPadding + line + rightPadding;
}

short[] LineToShorts(string line)
{
    var values = new short[4];
    var whitespace = new string(Enumerable.Repeat('0', 8).ToArray());
    var fullLine = whitespace + PadOrTrimLine(line) + whitespace;
    for (var i = 0; i < 4; i++)
    {
        var ind = i * 16;
        var shortString = new string(fullLine[ind..(ind + 16)].Reverse().ToArray());
        values[i] = Convert.ToInt16(shortString, 2);
    }
    return values;
}

List<short> FlipAnimationTopBottomData()
{
    const string start = "0000000011111111";
    var startReverse = new string(start.Reverse().ToArray());
    var startNum = Convert.ToInt16(start, 2);
    var startReverseNum = Convert.ToInt16(startReverse, 2);
    List<short> list =
    [
        startReverseNum,
        startNum
    ];
    for (var i = 0; i < 2; i++)
    {
        while (startNum != 0)
        {
            startNum = (short)((ushort)startNum >>> 2);
            startReverseNum = (short)((ushort)startReverseNum << 2);
            list.Add(startReverseNum);
            list.Add(startNum);
        }
        startNum = -1;
        startReverseNum = -1;
    }
    return list;
}

List<short> FlipAnimationSidesData()
{
    const string start = "0000000011000000";
    var startReverse = new string(start.Reverse().ToArray());
    var startNum = Convert.ToInt16(start, 2);
    var startReverseNum = Convert.ToInt16(startReverse, 2);
    List<short> list =
    [
        startReverseNum,
        startNum
    ];
    for (var i = 0; i < 2; i++)
    {
        while (startNum != 0)
        {
            startNum = (short)((ushort)startNum >>> 2);
            startReverseNum = (short)((ushort)startReverseNum << 2);
            list.Add(startReverseNum);
            list.Add(startNum);
        }
        startNum = -16384;
        startReverseNum = 3;
    }
    return list;
}