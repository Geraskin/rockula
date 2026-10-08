using RockULA.Headless;

if (args.Length == 0 || (args.Length == 1 && args[0] == "--about"))
{
    Console.WriteLine("RockULA! - Rock Your Spectrum.");
    Console.WriteLine("Status: initial Z80 NOP/load slice. Spectrum devices and game loading are not implemented.");
    return 0;
}

if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
{
    Console.WriteLine("Usage: RockULA.Headless [--about | --help | --demo]");
    Console.WriteLine("--demo executes a self-authored six-instruction Z80 load program without firmware.");
    return 0;
}

if (args.Length == 1 && args[0] == "--demo")
{
    return CpuDemo.Run();
}

Console.Error.WriteLine("Unsupported arguments. Use --help for the available commands.");
return 2;
