using System.Security.Cryptography;
using RockULA.Core;
using RockULA.Core.Input;
using RockULA.Core.Machines;
using RockULA.Core.Video;

namespace RockULA.Headless;

/// <summary>
/// Explicitly-invoked external oracle run: boots one user-supplied 16 KiB ROM on the existing
/// 48K machine, applies a bounded key script and reports deterministic output. No oracle asset
/// is downloaded or committed; see docs/decisions/0016-external-oracle.md.
/// </summary>
internal static class OracleRunner
{
    private const int DefaultFrames = 600;
    private const int MaximumFrames = 100000;
    private const int DefaultSettleFrames = 100;
    private const int MaximumSettleFrames = 100000;
    private const int MaximumKeys = 64;
    private const int KeyHoldFrames = 8;
    private const int KeyReleaseFrames = 2;
    private const int SmokeFrames = 60;

    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Use --oracle-rom ROM [--settle N] [--keys TOKENS] [--frames N] [--png PATH].");
            return 2;
        }

        string romPath = args[1];
        int frames = DefaultFrames;
        int settle = DefaultSettleFrames;
        string? keyScript = null;
        string? pngPath = null;

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--frames" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out frames) || frames is < 1 or > MaximumFrames)
                    {
                        Console.Error.WriteLine($"--frames must be an integer in 1..{MaximumFrames}.");
                        return 2;
                    }

                    break;
                case "--settle" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out settle) || settle is < 0 or > MaximumSettleFrames)
                    {
                        Console.Error.WriteLine($"--settle must be an integer in 0..{MaximumSettleFrames}.");
                        return 2;
                    }

                    break;
                case "--keys" when i + 1 < args.Length:
                    keyScript = args[++i];
                    break;
                case "--png" when i + 1 < args.Length:
                    pngPath = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unrecognised oracle argument '{args[i]}'. Use --oracle-rom ROM [--settle N] [--keys TOKENS] [--frames N] [--png PATH].");
                    return 2;
            }
        }

        SpectrumKey[] keys = Array.Empty<SpectrumKey>();
        if (keyScript is not null && !TryParseKeys(keyScript, out keys, out string keyError))
        {
            Console.Error.WriteLine(keyError);
            return 2;
        }

        string source = romPath;
        try
        {
            var rom = RomImage.FromSpectrum48(ReadBounded(romPath, RomImage.Spectrum48Size));
            var machine = new Spectrum48Machine(rom);
            for (int frame = 0; frame < settle; frame++) machine.RunFrame();

            if (keys.Length > 0)
            {
                foreach (SpectrumKey key in keys)
                {
                    machine.Bus.SetKey(key, true);
                    for (int frame = 0; frame < KeyHoldFrames; frame++) machine.RunFrame();
                    machine.Bus.SetKey(key, false);
                    for (int frame = 0; frame < KeyReleaseFrames; frame++) machine.RunFrame();
                }
            }

            for (int frame = 0; frame < frames; frame++) machine.RunFrame();

            string report = Describe(machine, out uint[] pixels);
            if (pngPath is not null)
            {
                // Written before the report line so a failed write cannot be preceded by a result.
                source = pngPath;
                PngImage.Write(pngPath, pixels, Spectrum48Video.Width, Spectrum48Video.Height);
            }

            Console.WriteLine(report);
            if (pngPath is not null)
            {
                Console.WriteLine($"wrote {pngPath}");
            }

            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"{source}: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Tool-path smoke: runs the oracle report on a self-authored dummy ROM so CI can exercise the
    /// command without an external asset. The dummy buffer is not firmware and proves nothing
    /// about machine accuracy.
    /// </summary>
    public static int Smoke()
    {
        var machine = new Spectrum48Machine(RomImage.FromSpectrum48(new byte[RomImage.Spectrum48Size]));
        for (int frame = 0; frame < SmokeFrames; frame++) machine.RunFrame();
        Console.WriteLine(Describe(machine, out _));
        return 0;
    }

    private static string Describe(Spectrum48Machine machine, out uint[] pixels)
    {
        pixels = new uint[Spectrum48Video.Width * Spectrum48Video.Height];
        Spectrum48Video.Render(machine.Bus, pixels, machine.FrameNumber);
        byte[] raw = new byte[pixels.Length * sizeof(uint)];
        Buffer.BlockCopy(pixels, 0, raw, 0, raw.Length);
        string hash = Convert.ToHexString(SHA256.HashData(raw));
        return $"frames={machine.FrameNumber} T={machine.Bus.TStates} PC={machine.Cpu.Registers.PC:X4} border={machine.Bus.BorderColor} FramebufferSHA256={hash}";
    }

    private static bool TryParseKeys(string script, out SpectrumKey[] keys, out string error)
    {
        string[] tokens = script.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is 0 or > MaximumKeys)
        {
            keys = Array.Empty<SpectrumKey>();
            error = $"--keys must list 1..{MaximumKeys} comma-separated tokens.";
            return false;
        }

        keys = new SpectrumKey[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim();
            keys[i] = token switch
            {
                "0" => SpectrumKey.D0,
                "1" => SpectrumKey.D1,
                "2" => SpectrumKey.D2,
                "3" => SpectrumKey.D3,
                "4" => SpectrumKey.D4,
                "5" => SpectrumKey.D5,
                "6" => SpectrumKey.D6,
                "7" => SpectrumKey.D7,
                "8" => SpectrumKey.D8,
                "9" => SpectrumKey.D9,
                "enter" => SpectrumKey.Enter,
                "space" => SpectrumKey.Space,
                "h" => SpectrumKey.H,
                "p" => SpectrumKey.P,
                "r" => SpectrumKey.R,
                "i" => SpectrumKey.I,
                "n" => SpectrumKey.N,
                "t" => SpectrumKey.T,
                _ => (SpectrumKey)(-1),
            };

            if ((int)keys[i] < 0)
            {
                error = $"Unknown --keys token '{token}'. Use digits 0-9, enter, space or h/p/r/i/n/t.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        using var input = File.OpenRead(path);
        if (input.Length > maximum) throw new ArgumentException($"file exceeds the {maximum}-byte input limit.");
        byte[] bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1) throw new ArgumentException("file changed while reading.");
        return bytes;
    }
}
