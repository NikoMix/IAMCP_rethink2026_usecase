using Azure.AI.Projects;
using Azure.Identity;
using ProposalGenerator.Knowledge;

var commandLine = CommandLine.Parse(args, Environment.GetEnvironmentVariable, out var error);
if (commandLine is null)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine();
    Console.Error.WriteLine(CommandLine.Usage);
    return 2;
}

if (commandLine.Help)
{
    Console.WriteLine(CommandLine.Usage);
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    var files = await KnowledgeSource.DiscoverAsync(commandLine.Source, cancellation.Token);
    if (commandLine.LocalOnly)
    {
        foreach (var file in files)
        {
            Console.WriteLine($"{file.ContentSha256}  {file.Content.Length,8}  {file.SourcePath}");
        }

        Console.WriteLine($"{files.Count} file(s) in '{Path.GetFullPath(commandLine.Source)}'.");
        return 0;
    }

    var gateway = new FoundryVectorStoreGateway(new AIProjectClient(commandLine.Endpoint!, new DefaultAzureCredential()));
    var report = await new KnowledgeIngestor(gateway).IngestAsync(files, commandLine.Ingestion, cancellation.Token);
    foreach (var line in ReportFormatter.Format(report, commandLine.Ingestion.VectorStoreName))
    {
        Console.WriteLine(line);
    }

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}
catch (Exception exception) when (exception is DirectoryNotFoundException or InvalidDataException or InvalidOperationException
    or TimeoutException or AuthenticationFailedException or System.ClientModel.ClientResultException)
{
    Console.Error.WriteLine($"Ingestion failed: {exception.Message}");
    return 1;
}
