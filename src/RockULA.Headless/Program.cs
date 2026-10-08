if (args.Length == 0 || (args.Length == 1 && args[0] == "--about"))
{
    Console.WriteLine("RockULA! - Rock Your Spectrum.");
    Console.WriteLine("Status: foundation. CPU, video, audio and game loading are not implemented.");
    return 0;
}

if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
{
    Console.WriteLine("Usage: RockULA.Headless [--about | --help]");
    Console.WriteLine("This foundation provides status only; execution commands arrive in later milestones.");
    return 0;
}

Console.Error.WriteLine("Unsupported arguments. Use --help for the available commands.");
return 2;
