using System.Diagnostics;

if (args.Length == 0)
{
    Console.WriteLine("rfs - Rufus CLI proof of concept");
    Console.WriteLine("Usage:");
    Console.WriteLine("  rfs --version");
    Console.WriteLine("  rfs pi \"message\"");
    return 0;
}

if (args[0] == "--version")
{
    Console.WriteLine("rfs 0.0.1-poc");
    return 0;
}

if (args[0] == "pi")
{
    var message = string.Join(" ", args.Skip(1));

    if (string.IsNullOrWhiteSpace(message))
    {
        Console.Error.WriteLine("Missing message.");
        return 1;
    }

    var psi = new ProcessStartInfo
    {
        FileName = "pi",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    psi.ArgumentList.Add(message);

    Process? process;

    try
    {
        process = Process.Start(psi);
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Failed to start pi.");
        return 1;
    }

    if (process is null)
    {
        Console.Error.WriteLine("Failed to start pi.");
        return 1;
    }

    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();

    await process.WaitForExitAsync();

    await Console.Out.WriteAsync(await stdoutTask);
    await Console.Error.WriteAsync(await stderrTask);

    return process.ExitCode;
}

Console.Error.WriteLine($"Unknown command: {args[0]}");
return 1;
