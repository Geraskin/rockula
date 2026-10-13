using System.Globalization;
using RockULA.Core.Cpu;

namespace RockULA.Headless;

/// <summary>
/// Explicitly-invoked CPU corpus runner for the Fuse Z80 test format
/// (see docs/testing.md#external-oracle-suite). It reads a user-supplied corpus, executes one
/// instruction per test on a flat 64 KiB bus and compares registers, T-states and memory.
/// ULA contention events (MC/PC) are ignored because this machine does not model contention.
/// Runs one instruction (or halted fetch) per test, so the corpus cannot hang the host.
/// </summary>
internal static class FuseTestSuite
{
    private const int MaximumFileBytes = 4 * 1024 * 1024;
    private const int MaximumReportedFailures = 20;

    /// <summary>
    /// Fuse's own corpus disagrees with the documented result for the undocumented X/Y bits after
    /// BIT n,(HL). The reference C integration (floooh/chips-test, tests/z80-fuse.c) blacklists
    /// exactly these names and states that "FUSE handles those wrong", so they are compared with
    /// X/Y masked and reported in their own count, never as a pass.
    /// </summary>
    private static readonly string[] KnownXyDivergences =
    {
        "cb46", "cb4e", "cb56", "cb5e", "cb66", "cb6e", "cb76", "cb7e",
    };

    public static int Run(string inputPath, string expectedPath)
    {
        try
        {
            string[] inputLines = ReadLines(inputPath);
            string[] expectedLines = ReadLines(expectedPath);
            var tests = Parse(inputLines, expectedLines);
            if (tests.Count == 0)
            {
                Console.Error.WriteLine("The corpus contains no test blocks.");
                return 1;
            }

            int passed = 0, failed = 0, skipped = 0, knownDivergence = 0, reported = 0;
            foreach (var test in tests)
            {
                if (test.Input.Halted)
                {
                    // The CPU enters HALT only by executing HALT; a halted start is not representable.
                    skipped++;
                    continue;
                }

                bool known = KnownXyDivergences.Contains(test.Name);
                string? failure = Execute(test, known);
                if (failure is null)
                {
                    if (known)
                    {
                        knownDivergence++;
                    }
                    else
                    {
                        passed++;
                    }
                }
                else
                {
                    failed++;
                    if (reported < MaximumReportedFailures)
                    {
                        reported++;
                        Console.WriteLine($"FAIL {test.Name}: {failure}");
                    }
                }
            }

            Console.WriteLine($"tests={tests.Count} passed={passed} failed={failed} knownXyDivergence={knownDivergence} skipped={skipped} (contention events not compared)");
            return failed == 0 ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string? Execute(FuseTest test, bool maskXy)
    {
        var bus = new FlatTestBus();
        foreach (var (address, bytes) in test.InputMemory) bus.Load(address, bytes);

        var registers = new Z80Registers
        {
            AF = test.Input.Af,
            BC = test.Input.Bc,
            DE = test.Input.De,
            HL = test.Input.Hl,
            AlternateAF = test.Input.Af2,
            AlternateBC = test.Input.Bc2,
            AlternateDE = test.Input.De2,
            AlternateHL = test.Input.Hl2,
            IX = test.Input.Ix,
            IY = test.Input.Iy,
            SP = test.Input.Sp,
            PC = test.Input.Pc,
            I = test.Input.I,
            R = test.Input.R,
            Iff1 = test.Input.Iff1 != 0,
            Iff2 = test.Input.Iff2 != 0,
            InterruptMode = test.Input.Im,
        };
        var cpu = new Z80Cpu(bus, registers);

        try
        {
            do
            {
                cpu.Step();
            }
            while (bus.TStates < (ulong)test.Input.TStates);
        }
        catch (Exception exception)
        {
            return exception.Message;
        }

        foreach (string mismatch in Compare(test, cpu, bus, maskXy)) return mismatch;
        return null;
    }

    private static IEnumerable<string> Compare(FuseTest test, Z80Cpu cpu, FlatTestBus bus, bool maskXy)
    {
        ushort mask = maskXy ? (ushort)0xFFD7 : (ushort)0xFFFF;
        var expected = test.Expected;
        var actual = cpu.Registers;

        if (expected.TStates != (int)bus.TStates) yield return $"tstates {bus.TStates} != {expected.TStates}";
        if ((expected.Af & mask) != (actual.AF & mask)) yield return $"AF {actual.AF:X4} != {expected.Af:X4}";
        if (expected.Bc != actual.BC) yield return $"BC {actual.BC:X4} != {expected.Bc:X4}";
        if (expected.De != actual.DE) yield return $"DE {actual.DE:X4} != {expected.De:X4}";
        if (expected.Hl != actual.HL) yield return $"HL {actual.HL:X4} != {expected.Hl:X4}";
        if (expected.Af2 != actual.AlternateAF) yield return $"AF' {actual.AlternateAF:X4} != {expected.Af2:X4}";
        if (expected.Bc2 != actual.AlternateBC) yield return $"BC' {actual.AlternateBC:X4} != {expected.Bc2:X4}";
        if (expected.De2 != actual.AlternateDE) yield return $"DE' {actual.AlternateDE:X4} != {expected.De2:X4}";
        if (expected.Hl2 != actual.AlternateHL) yield return $"HL' {actual.AlternateHL:X4} != {expected.Hl2:X4}";
        if (expected.Ix != actual.IX) yield return $"IX {actual.IX:X4} != {expected.Ix:X4}";
        if (expected.Iy != actual.IY) yield return $"IY {actual.IY:X4} != {expected.Iy:X4}";
        if (expected.Sp != actual.SP) yield return $"SP {actual.SP:X4} != {expected.Sp:X4}";
        if (expected.Pc != actual.PC) yield return $"PC {actual.PC:X4} != {expected.Pc:X4}";
        if (expected.I != actual.I) yield return $"I {actual.I:X2} != {expected.I:X2}";
        if (expected.R != actual.R) yield return $"R {actual.R:X2} != {expected.R:X2}";
        if ((expected.Iff1 != 0) != actual.Iff1) yield return $"IFF1 {actual.Iff1} != {expected.Iff1}";
        if ((expected.Iff2 != 0) != actual.Iff2) yield return $"IFF2 {actual.Iff2} != {expected.Iff2}";
        if (expected.Im != actual.InterruptMode) yield return $"IM {actual.InterruptMode} != {expected.Im}";
        if (expected.Halted != cpu.IsHalted) yield return $"halted {cpu.IsHalted} != {expected.Halted}";

        foreach (var (address, bytes) in test.ExpectedMemory)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                ushort at = (ushort)((address + i) & 0xFFFF);
                byte actualByte = bus.Peek(at);
                if (actualByte != bytes[i]) yield return $"mem {at:X4} {actualByte:X2} != {bytes[i]:X2}";
            }
        }
    }

    private static List<FuseTest> Parse(string[] inputLines, string[] expectedLines)
    {
        var tests = new List<FuseTest>();
        int inputIndex = 0, expectedIndex = 0;
        while (inputIndex < inputLines.Length && expectedIndex < expectedLines.Length)
        {
            SkipBlanks(inputLines, ref inputIndex);
            SkipBlanks(expectedLines, ref expectedIndex);
            if (inputIndex >= inputLines.Length || expectedIndex >= expectedLines.Length) break;

            string name = inputLines[inputIndex++].Trim();
            string expectedName = expectedLines[expectedIndex++].Trim();
            if (!string.Equals(name, expectedName, StringComparison.Ordinal))
            {
                throw new FormatException($"Corpus mismatch: input '{name}' but expected '{expectedName}'.");
            }

            var input = ParseState(inputLines, ref inputIndex);
            var inputMemory = ParseMemory(inputLines, ref inputIndex);
            if (inputIndex >= inputLines.Length || inputLines[inputIndex++].Trim() != "-1")
            {
                throw new FormatException($"Test '{name}': the input block is not terminated by -1.");
            }

            SkipEvents(expectedLines, ref expectedIndex);
            var expected = ParseState(expectedLines, ref expectedIndex);
            var expectedMemory = ParseMemory(expectedLines, ref expectedIndex);
            tests.Add(new FuseTest(name, input, inputMemory, expected, expectedMemory));
        }

        return tests;
    }

    private static FuseState ParseState(string[] lines, ref int index)
    {
        if (index + 1 >= lines.Length) throw new FormatException("Corpus ended inside a test block.");
        ushort[] pairs = ParseNumbers(lines[index++], 12, ushort.Parse);
        if (pairs.Length != 12) throw new FormatException("A register line must contain 12 words.");
        string[] state = SplitTokens(lines[index++]);
        if (state.Length != 7) throw new FormatException("A state line must contain I, R, IFF1, IFF2, IM, halted and tstates.");
        return new FuseState(
            pairs[0], pairs[1], pairs[2], pairs[3], pairs[4], pairs[5], pairs[6], pairs[7], pairs[8], pairs[9], pairs[10], pairs[11],
            byte.Parse(state[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(state[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(state[2], CultureInfo.InvariantCulture),
            byte.Parse(state[3], CultureInfo.InvariantCulture),
            byte.Parse(state[4], CultureInfo.InvariantCulture),
            state[5] != "0",
            int.Parse(state[6], CultureInfo.InvariantCulture));
    }

    private static List<(ushort Address, byte[] Bytes)> ParseMemory(string[] lines, ref int index)
    {
        var chunks = new List<(ushort, byte[])>();
        while (index < lines.Length)
        {
            string trimmed = lines[index].Trim();
            if (trimmed.Length == 0) break;
            if (trimmed == "-1")
            {
                // Input blocks terminate with a standalone -1 line; expected blocks simply end.
                break;
            }

            index++;
            string[] tokens = SplitTokens(trimmed);
            if (tokens.Length < 3 || tokens[^1] != "-1")
            {
                throw new FormatException($"Malformed memory line '{trimmed}'.");
            }

            ushort address = ushort.Parse(tokens[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var bytes = new byte[tokens.Length - 2];
            for (int i = 1; i < tokens.Length - 1; i++)
            {
                bytes[i - 1] = byte.Parse(tokens[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            chunks.Add((address, bytes));
        }

        return chunks;
    }

    private static void SkipEvents(string[] lines, ref int index)
    {
        while (index < lines.Length && IsEventLine(lines[index])) index++;
    }

    private static bool IsEventLine(string line)
    {
        string[] tokens = SplitTokens(line);
        return tokens.Length >= 3 && tokens[1] is "MC" or "MR" or "MW" or "PR" or "PW" or "PC";
    }

    private static ushort[] ParseNumbers(string line, int expected, Func<string, NumberStyles, IFormatProvider, ushort> parse)
    {
        string[] tokens = SplitTokens(line);
        if (tokens.Length != expected) throw new FormatException($"Expected {expected} words but found {tokens.Length} in '{line.Trim()}'.");
        var values = new ushort[expected];
        for (int i = 0; i < expected; i++) values[i] = parse(tokens[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return values;
    }

    private static string[] SplitTokens(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void SkipBlanks(string[] lines, ref int index)
    {
        while (index < lines.Length && lines[index].Trim().Length == 0) index++;
    }

    private static string[] ReadLines(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException($"Corpus file '{path}' was not found.");
        if (info.Length > MaximumFileBytes) throw new ArgumentException($"Corpus file '{path}' exceeds the {MaximumFileBytes}-byte bound.");
        return File.ReadAllLines(path);
    }

    private sealed record FuseState(
        ushort Af, ushort Bc, ushort De, ushort Hl, ushort Af2, ushort Bc2, ushort De2, ushort Hl2,
        ushort Ix, ushort Iy, ushort Sp, ushort Pc, byte I, byte R, byte Iff1, byte Iff2, byte Im,
        bool Halted, int TStates);

    private sealed record FuseTest(
        string Name,
        FuseState Input,
        IReadOnlyList<(ushort Address, byte[] Bytes)> InputMemory,
        FuseState Expected,
        IReadOnlyList<(ushort Address, byte[] Bytes)> ExpectedMemory);

    /// <summary>Flat 64 KiB memory with the Fuse DE AD BE EF fill and port reads returning the high byte.</summary>
    private sealed class FlatTestBus : Z80Bus
    {
        private readonly byte[] _memory = new byte[65536];

        public FlatTestBus()
        {
            for (int i = 0; i < _memory.Length; i += 4)
            {
                _memory[i] = 0xDE;
                _memory[i + 1] = 0xAD;
                _memory[i + 2] = 0xBE;
                _memory[i + 3] = 0xEF;
            }
        }

        public void Load(ushort address, ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++) _memory[(address + i) & 0xFFFF] = bytes[i];
        }

        public byte Peek(ushort address) => _memory[address];

        protected override byte ReadMemory(ushort address) => _memory[address];

        protected override void WriteMemory(ushort address, byte value) => _memory[address] = value;

        protected override byte ReadPort(ushort address) => (byte)(address >> 8);
    }
}
