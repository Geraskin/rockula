using System.Security.Cryptography;
using RockULA.Core;
using RockULA.Core.Input;
using RockULA.Core.Machines;
using RockULA.Core.Video;
using RockULA.Formats;

namespace RockULA.Headless;

internal static class SnapshotRunner
{
    public static int Run(string romPath, string snapshotPath, int frames)
    {
        try
        {
            var rom = RomImage.FromSpectrum48(ReadBounded(romPath, 16384));
            var snapshot = SpectrumSnapshotReader.Parse(Path.GetExtension(snapshotPath), ReadBounded(snapshotPath, SpectrumSnapshotReader.MaximumFileSize));
            var machine = new Spectrum48Machine(rom);
            machine.LoadSnapshot(snapshot.CpuState, snapshot.Ram, snapshot.BorderColor);
            for (int frame = 0; frame < frames; frame++) machine.RunFrame();
            uint[] pixels = new uint[320 * 240];
            Spectrum48Video.Render(machine.Bus, pixels, machine.FrameNumber);
            byte[] bytes = new byte[pixels.Length * 4];
            Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);
            Console.WriteLine($"Frames={machine.FrameNumber} T={machine.Bus.TStates} PC={machine.Cpu.Registers.PC:X4} FrameSHA256={Convert.ToHexString(SHA256.HashData(bytes))}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
    private static byte[] ReadBounded(string path, int maximum)
    {
        using var input = File.OpenRead(path);
        if (input.Length > maximum) throw new ArgumentException("File exceeds the selected 48K input size.");
        byte[] bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1) throw new ArgumentException("File changed while reading.");
        return bytes;
    }
    public static int Demo()
    {
        // Self-authored snapshot and dummy ROM; no downloaded firmware or game.
        byte[] sna = new byte[Sna48Snapshot.FileSize];
        sna[24] = 0x90;
        sna[25] = 1;
        sna[27 + 0x5001] = 0x80;
        byte[] code = [0x3E, 7, 0x32, 0, 0x58, 0x3E, 0x80, 0x32, 0, 0x40,
            0x3E, 2, 0xD3, 0xFE, 0xDB, 0xFE, 0x32, 0, 0x60, 0x18, 0xF5];
        code.CopyTo(sna, 27 + 0x4000);
        var snapshot = Sna48Snapshot.Parse(sna);
        var machine = new Spectrum48Machine(RomImage.FromSpectrum48(new byte[16384]));
        machine.LoadSnapshot(snapshot.CpuState, snapshot.Ram, 0);
        machine.Bus.SetKey(SpectrumKey.Space, true);
        for (int frame = 0; frame < 3; frame++) machine.RunFrame();
        uint[] pixels = new uint[320 * 240];
        Spectrum48Video.Render(machine.Bus, pixels, machine.FrameNumber);
        bool valid = machine.Bus.Peek(0x6000) == 0xFE && machine.Bus.BorderColor == 2
            && pixels[24 * 320 + 32] == 0xFFCDCDCD && machine.FrameNumber == 3;
        Console.WriteLine($"Synthetic SNA/machine demo: frames={machine.FrameNumber} keyboard={machine.Bus.Peek(0x6000):X2} border={machine.Bus.BorderColor} pixel={pixels[24 * 320 + 32]:X8} T={machine.Bus.TStates}");
        return valid ? 0 : 1;
    }
}
