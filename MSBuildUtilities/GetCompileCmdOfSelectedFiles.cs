using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Gaijin.MSBuild.Utilities;

public sealed class GetCompileCmdOfSelectedFiles : Microsoft.Build.Utilities.Task
{
    [Required]
    public string BuildCommand { get; set; }
    [Required]
    public string WorkDirectory { get; set; }
    [Required]
    public string[] SelectedFiles { get; set; }
    public string[] ExcludeArgs { get; set; }

    [Output]
    public ITaskItem[] Commands { get; set; }
    [Output]
    public string SkippedFiles { get; set; }

    public sealed override bool Execute()
    {
        List<FileDesc> selectedFiles = [];
        for (int i = 0; i < SelectedFiles.Length; i++)
        {
            selectedFiles.Add(new FileDesc(SelectedFiles[i].Replace('\\', '/'), i));
        }

        List<TaskItem> commands = [];

        void runJam(Process jamBuild)
        {
            jamBuild.StartInfo.UseShellExecute = false;
            jamBuild.StartInfo.RedirectStandardOutput = true;
            jamBuild.StartInfo.FileName = "jam";
            jamBuild.StartInfo.WorkingDirectory = WorkDirectory;

            jamBuild.OutputDataReceived += (s, e) =>
            {
                bool filtered = false;
                var line = e.Data.AsSpan();
                if (line.StartsWith("  call_filtered ".AsSpan()))
                {
                    line = line.Slice(16, line.Length - 19);
                    filtered = true;
                }
                else if (line.StartsWith("  call ".AsSpan()))
                {
                    line = line.Slice(7);
                }
                else
                {
                    return;
                }

                for (int i = 0; i < selectedFiles.Count; i++)
                {
                    if (line.EndsWith(selectedFiles[i].File.AsSpan()))
                    {
                        Span<char> command = stackalloc char[line.Length];
                        line.CopyTo(command);

                        for (int j = 0; j < ExcludeArgs.Length; j++)
                            for (int index = command.IndexOf(ExcludeArgs[j].AsSpan()), k = index; k < index + ExcludeArgs[j].Length; k++)
                                command[k] = ' ';

                        if (filtered)
                        {
                            int index = command.IndexOf("#\\(".AsSpan());
                            command[index] = ' ';
                            command[index + 1] = ' ';
                            command[index + 2] = ' ';
                        }

                        TaskItem commandItem = new(command.ToString());
                        commandItem.SetMetadata("File", SelectedFiles[selectedFiles[i].Index]);
                        commands.Add(commandItem);
                        selectedFiles.RemoveAt(i);

                        if (selectedFiles.Count == 0)
                            jamBuild.Kill();

                        break;
                    }
                }
            };

            jamBuild.Start();
            jamBuild.BeginOutputReadLine();
            jamBuild.WaitForExit();
        }

        {
            using Process jamBuild = new();
            jamBuild.StartInfo.Arguments = BuildCommand + " -sSkipDigestProecssing -n -dx";
            runJam(jamBuild);
        }

        if (selectedFiles.Count > 0)
        {
            using Process jamBuild = new();
            jamBuild.StartInfo.Arguments = BuildCommand + " -sSkipDigestProecssing -n -dx -a";
            runJam(jamBuild);
        }

        Commands = [.. commands];
        SkippedFiles = string.Join("\n", selectedFiles);

        return true;
    }

    private readonly struct FileDesc(string file, int index)
    {
        public string File { get; } = file;
        public int Index { get; } = index;

        public override string ToString() => File;
    }
}
