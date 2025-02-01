using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

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
            selectedFiles.Add(new(SelectedFiles[i].Replace('\\', '/'), i));
        }

        List<TaskItem> commands = [];

        using (Process jamBuild = new()
        {
            StartInfo = new()
            {
                Arguments = BuildCommand + " -sDumpBuildCmd=yes -d0 -a -n",
                FileName = "jam",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = WorkDirectory,
            }
        })
        {
            var serializer = JsonSerializer.Create();

            jamBuild.Start();

            using JsonTextReader jsonReader = new(jamBuild.StandardOutput);

            jsonReader.Read(); // move to start of first object
            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
            {
                var compileCommand = serializer.Deserialize<CompileCommand>(jsonReader);
                for (int i = 0; i < selectedFiles.Count; i++)
                {
                    if (compileCommand.File.EndsWith(selectedFiles[i].File))
                    {
                        TaskItem commandItem = new(string.Join(" ", compileCommand.Arguments));
                        commandItem.SetMetadata("File", SelectedFiles[selectedFiles[i].Index]);
                        commands.Add(commandItem);
                        selectedFiles.RemoveAt(i);

                        if (selectedFiles.Count == 0)
                            goto KillJamBuild;

                        break;
                    }
                }
            }

        KillJamBuild:
            jamBuild.Kill();
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
