using System.Diagnostics;

if (args.Length == 0)
{
    Console.WriteLine("rfs - Rufus CLI proof of concept");
    Console.WriteLine("Usage:");
    Console.WriteLine("  rfs --version");
    Console.WriteLine("  rfs pi [message]");
    Console.WriteLine("  rfs ask \"message\"");
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

    var psi = new ProcessStartInfo
    {
        FileName = "pi",
        UseShellExecute = false
    };

    if (!string.IsNullOrWhiteSpace(message))
    {
        psi.ArgumentList.Add(message);
    }

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

    await process.WaitForExitAsync();
    return process.ExitCode;
}

if (args[0] == "ask")
{
    var prompt = string.Join(" ", args.Skip(1));

    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Error.WriteLine("Missing prompt.");
        return 1;
    }

    const string helperRelativePath = "tools/rfs/bridge/rfs-ask.mjs";
    var helperPath = FindRepoFile(helperRelativePath);
    if (helperPath is null)
    {
        Console.Error.WriteLine($"rfs ask helper not found: {helperRelativePath}");
        return 1;
    }

    var psi = new ProcessStartInfo
    {
        FileName = "node",
        UseShellExecute = false
    };

    psi.ArgumentList.Add(helperPath);
    psi.ArgumentList.Add(prompt);

    Process? process;

    try
    {
        process = Process.Start(psi);
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Failed to start rfs ask helper.");
        return 1;
    }

    if (process is null)
    {
        Console.Error.WriteLine("Failed to start rfs ask helper.");
        return 1;
    }

    await process.WaitForExitAsync();
    return process.ExitCode;
}

Console.Error.WriteLine($"Unknown command: {args[0]}");
return 1;

static string? FindRepoFile(string relativePath)
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var current = new DirectoryInfo(currentDirectory);

    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, relativePath);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return null;
}
