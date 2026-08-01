using Modeller.Cli;
using Modeller.Output;
using System.Collections.Immutable;
using System.Text.Json;

return (int)await CliApplication.RunAsync(args, new PhysicalCliHost(Environment.CurrentDirectory));

internal sealed class PhysicalCliHost(string workingDirectory) : ICliHost
{
    public TextWriter Output => Console.Out;
    public TextWriter Error => Console.Error;
    public bool Exists(string path) => File.Exists(Resolve(path));
    public async ValueTask<string> ReadTextAsync(string path, CancellationToken cancellationToken) =>
        await File.ReadAllTextAsync(Resolve(path), cancellationToken);
    public async ValueTask WriteTextAsync(string path, string content, bool overwrite, CancellationToken cancellationToken)
    {
        var target = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (!overwrite && File.Exists(target)) throw new IOException("The target already exists.");
        await File.WriteAllTextAsync(target, content, cancellationToken);
    }
    public async ValueTask ApplyOutputAsync(string root, ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken)
    {
        var recoveryRoot = Resolve($".modeller/recovery/{recoveryToken.Replace(':', '-')}");
        Directory.CreateDirectory(recoveryRoot);
        await File.WriteAllTextAsync(Path.Combine(recoveryRoot, "operations.json"), JsonSerializer.Serialize(operations), cancellationToken);
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Resolve($"{root.TrimEnd('/', '\\')}/{operation.Path}");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
            {
                var backup = Path.Combine(recoveryRoot, $"{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(operation.Path)))}.bak");
                File.Copy(target, backup, true);
            }
            if (operation.Kind == FileOperationKind.Delete) File.Delete(target);
            else
            {
                var temporary = Path.Combine(recoveryRoot, $"{Guid.NewGuid():N}.tmp");
                await File.WriteAllTextAsync(temporary, operation.Content!, cancellationToken);
                File.Move(temporary, target, true);
            }
        }
    }
    private string Resolve(string path)
    {
        var root = Path.GetFullPath(workingDirectory) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path, workingDirectory);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("The path leaves the workspace.");
        return target;
    }
}
