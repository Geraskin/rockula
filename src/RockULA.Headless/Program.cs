using RockULA.Headless;

if (args.Length == 0 || (args.Length == 1 && args[0] == "--about"))
{
    Console.WriteLine("RockULA! - Rock Your Spectrum.");
    Console.WriteLine("Status: functional Spectrum 48K SNA/Z80 player; complete opcode decoding, NMI/IM 1/2 and IM 0 RST responses. Final-RAM video, keyboard/Kempston/Sinclair and opt-in beeper PCM; no contention.");
    return 0;
}

if (args.Length == 1 && args[0] == "--demo-z80") return SnapshotRunner.DemoZ80();
if (args.Length == 1 && args[0] == "--demo-machine") return SnapshotRunner.Demo();
if (args.Length is 3 or 5 && args[0] is "--sna" or "--snapshot")
{
    int frames = 100;
    if (args.Length == 5 && (args[3] != "--frames" || !int.TryParse(args[4], out frames) || frames is < 1 or > 10000))
    {
        Console.Error.WriteLine("Use --snapshot ROM SNAPSHOT [--frames 1..10000].");
        return 2;
    }
    return SnapshotRunner.Run(args[1], args[2], frames);
}

if (args.Length >= 2 && args[0] == "--oracle-rom") return OracleRunner.Run(args);
if (args.Length == 1 && args[0] == "--oracle-smoke") return OracleRunner.Smoke();

if (args.Length == 3 && args[0] == "--oracle-fuse") return FuseTestSuite.Run(args[1], args[2]);

if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
{
    Console.WriteLine("Usage: RockULA.Headless [--about | --help | --demo | --demo-loop | --demo-machine | --demo-z80 | --snapshot ROM SNAPSHOT [--frames N] | --oracle-rom ROM [--settle N] [--keys TOKENS] [--frames N] [--png PATH] | --oracle-fuse IN EXPECTED | --oracle-smoke]");
    Console.WriteLine("--demo executes a self-authored six-instruction Z80 load program without firmware.");
    Console.WriteLine("--demo-loop sums 3+2+1 using a bounded loop, subroutine and stack without firmware.");
    Console.WriteLine("--snapshot ROM SNAPSHOT resumes a user-supplied 48K SNA/Z80 snapshot (--sna is an alias) for a bounded frame count.");
    Console.WriteLine("--oracle-rom boots one user-supplied 16 KiB ROM, applies an optional --keys script after --settle frames, and reports the framebuffer SHA-256; no oracle asset is bundled.");
    Console.WriteLine("--oracle-smoke runs the oracle report on a self-authored dummy ROM so CI can check the command path without an external asset.");
    Console.WriteLine("--oracle-fuse runs a user-supplied Fuse Z80 corpus (IN EXPECTED), comparing registers, T-states and memory; contention events are not compared.");
    return 0;
}

if (args.Length == 1 && args[0] == "--demo")
{
    return CpuDemo.Run();
}

if (args.Length == 1 && args[0] == "--demo-loop")
{
    return CpuDemo.RunLoop();
}

Console.Error.WriteLine("Unsupported arguments. Use --help for the available commands.");
return 2;
