using RockULA.Core.Cpu;

namespace RockULA.Headless;

internal static class CpuDemo
{
    public static int RunLoop()
    {
        var bus = new DemoBus();
        // Sum 3+2+1 through a subroutine; preserve BC with PUSH/POP.
        byte[] program = [0x31, 0x00, 0x80, 0x06, 0x03, 0xAF, 0xCD, 0x20, 0x00,
            0x10, 0xFB, 0x32, 0x00, 0x40, 0xC3, 0x12, 0x00];
        program.CopyTo(bus.Memory, 0);
        byte[] subroutine = [0xC5, 0x80, 0xC1, 0xC9];
        subroutine.CopyTo(bus.Memory, 0x20);
        var cpu = new Z80Cpu(bus);
        Console.WriteLine("RockULA! synthetic Z80 loop/subroutine demo (flat RAM, no Spectrum devices).");
        int instructions = 0;
        while (cpu.Registers.PC != 0x12 && instructions < 64)
        {
            ushort pc = cpu.Registers.PC;
            ulong cost = cpu.Step();
            instructions++;
            Console.WriteLine($"PC={pc:X4} -> PC={cpu.Registers.PC:X4} A={cpu.Registers.A:X2} "
                + $"B={cpu.Registers.B:X2} SP={cpu.Registers.SP:X4} +{cost} T={bus.TStates}");
        }
        Console.WriteLine($"Result: PC={cpu.Registers.PC:X4} A={cpu.Registers.A:X2} F={cpu.Registers.F:X2} "
            + $"BC={cpu.Registers.BC:X4} SP={cpu.Registers.SP:X4} R={cpu.Registers.R:X2} "
            + $"RAM[4000]={bus.Memory[0x4000]:X2} instructions={instructions} T={bus.TStates}");
        bool expected = instructions == 23 && bus.TStates == 234 && cpu.Registers.PC == 0x12
            && cpu.Registers.A == 6 && cpu.Registers.F == 0 && cpu.Registers.BC == 0
            && cpu.Registers.SP == 0x8000 && cpu.Registers.R == 23 && bus.Memory[0x4000] == 6;
        if (!expected) Console.Error.WriteLine("The bounded loop demo did not produce its documented result.");
        return expected ? 0 : 1;
    }

    public static int Run()
    {
        var bus = new DemoBus();
        // Self-authored guest code; no firmware or game image is required.
        byte[] program = [0x21, 0x00, 0x40, 0x36, 0x2A, 0x7E, 0x32, 0x01, 0x40, 0x06, 0x03, 0x00];
        program.CopyTo(bus.Memory, 0);
        var cpu = new Z80Cpu(bus);
        Console.WriteLine("RockULA! synthetic Z80 load demo (six instructions, no Spectrum devices).");

        for (int i = 0; i < 6; i++)
        {
            ushort pc = cpu.Registers.PC;
            byte opcode = bus.Memory[pc];
            ulong cost = cpu.Step();
            Console.WriteLine(
                $"PC={pc:X4} OP={opcode:X2} -> PC={cpu.Registers.PC:X4} "
                + $"A={cpu.Registers.A:X2} B={cpu.Registers.B:X2} HL={cpu.Registers.HL:X4} "
                + $"+{cost} T={bus.TStates}");
        }

        Console.WriteLine(
            $"Result: PC={cpu.Registers.PC:X4} A={cpu.Registers.A:X2} B={cpu.Registers.B:X2} "
            + $"HL={cpu.Registers.HL:X4} R={cpu.Registers.R:X2} "
            + $"RAM[4000]={bus.Memory[0x4000]:X2} RAM[4001]={bus.Memory[0x4001]:X2} T={bus.TStates}");

        bool expected = cpu.Registers.PC == 12
            && cpu.Registers.A == 0x2A
            && cpu.Registers.B == 3
            && cpu.Registers.HL == 0x4000
            && cpu.Registers.R == 6
            && bus.Memory[0x4000] == 0x2A
            && bus.Memory[0x4001] == 0x2A
            && bus.TStates == 51;

        if (!expected)
        {
            Console.Error.WriteLine("The synthetic demo did not produce its documented result.");
            return 1;
        }

        return 0;
    }

    // Synthetic flat RAM for this demo only; this is not the Spectrum 48K address map.
    private sealed class DemoBus : Z80Bus
    {
        public byte[] Memory { get; } = new byte[65536];

        protected override byte ReadMemory(ushort address) => Memory[address];

        protected override void WriteMemory(ushort address, byte value)
        {
            Memory[address] = value;
        }
    }
}
