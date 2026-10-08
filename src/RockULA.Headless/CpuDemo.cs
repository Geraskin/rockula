using RockULA.Core.Cpu;

namespace RockULA.Headless;

internal static class CpuDemo
{
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
