using RockULA.Headless;

if (args.Length == 0 || (args.Length == 1 && args[0] == "--about"))
{
    Console.WriteLine("RockULA! - Rock Your Spectrum.");
    Console.WriteLine("Status: 248 Z80 base encodings, 256 CB payloads and six ED commands and NMI/IM 1/2 responses. Spectrum devices are not implemented.");
    return 0;
}

if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
{
    Console.WriteLine("Usage: RockULA.Headless [--about | --help | --demo | --demo-loop]");
    Console.WriteLine("--demo executes a self-authored six-instruction Z80 load program without firmware.");
    Console.WriteLine("--demo-loop sums 3+2+1 using a bounded loop, subroutine and stack without firmware.");
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
