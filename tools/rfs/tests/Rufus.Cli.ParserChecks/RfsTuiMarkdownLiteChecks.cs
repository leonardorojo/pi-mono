using Rufus.Cli.Tui;

internal static class RfsTuiMarkdownLiteChecks
{
    internal static void Run(List<string> failures)
    {
        RunMarkdownLiteRenderCases(failures);
        RunWriteResponseCases(failures);
    }

    private static void RunMarkdownLiteRenderCases(List<string> failures)
    {
        const string plainInput = "# Teorema\n\n## Demostración\n\n- item uno\n* item dos\n1. paso uno\n2. paso dos\n\n`inline code` y **texto** con a^2 + b^2 = c^2\n\nTexto normal";
        const string fenceInput = "```csharp\nif (x < y)\n{\n    return \"## no heading, **not bold**, a^2\";\n}\n```";

        var rendered = RfsTuiMarkdownLiteRenderer.Render(plainInput, useAnsi: false);
        var fencedRendered = RfsTuiMarkdownLiteRenderer.Render(fenceInput, useAnsi: false);

        if (rendered.Contains("# ", StringComparison.Ordinal) || rendered.Contains("## ", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected headings to render without hash markers in plain mode.");
        }

        if (!rendered.Contains("Teorema", StringComparison.Ordinal) || !rendered.Contains("Demostración", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected heading text to remain visible.");
        }

        if (!rendered.Contains("• item uno", StringComparison.Ordinal) || !rendered.Contains("• item dos", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected bullets to render as bullet characters.");
        }

        if (!rendered.Contains("1. paso uno", StringComparison.Ordinal) || !rendered.Contains("2. paso dos", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected numbered lists to remain legible.");
        }

        if (rendered.Contains("`", StringComparison.Ordinal) || rendered.Contains("**", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected inline markdown markers to be removed in plain mode.");
        }

        if (!rendered.Contains("a² + b² = c²", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected simple exponent notation to normalize to Unicode superscripts.");
        }

        if (!fencedRendered.Contains("```csharp", StringComparison.Ordinal) || !fencedRendered.Contains("## no heading, **not bold**, a^2", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected code fences to be preserved verbatim.");
        }

        if (!rendered.Contains("Texto normal", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected normal text to remain readable.");
        }

        var incomplete = RfsTuiMarkdownLiteRenderer.Render("## Heading\n\n**unfinished\n\n```\ncode", useAnsi: false);
        if (!incomplete.Contains("Heading", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected incomplete markdown to render safely.");
        }
    }

    private static void RunWriteResponseCases(List<string> failures)
    {
        var originalOut = Console.Out;
        using var stdout = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            RfsTuiRenderer.WriteResponse("# Título\n\n- item\n\nTexto con a^2 + b^2 = c^2");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = stdout.ToString();
        if (!output.Contains("Respuesta:", StringComparison.Ordinal) || !output.Contains("────────────────────────────────────────────", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected WriteResponse to keep the response heading and separator.");
        }

        if (output.Contains("# ", StringComparison.Ordinal) || output.Contains("- item", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected WriteResponse to pass the answer through markdown-lite rendering.");
        }

        if (!output.Contains("Título", StringComparison.Ordinal) || !output.Contains("• item", StringComparison.Ordinal) || !output.Contains("a² + b² = c²", StringComparison.Ordinal))
        {
            failures.Add("[tui markdown-lite] expected WriteResponse to render headings, bullets, and simple formulas.");
        }
    }
}
