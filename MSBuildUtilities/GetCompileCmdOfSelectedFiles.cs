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
                Arguments = BuildCommand + " -sDumpBuildCmds=yes -d0 -a -n",
                FileName = "jam",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = WorkDirectory,
            }
        })
        {
            // This parser expects the {"directory": ,"arguments": , "output": "file": } line format
            jamBuild.OutputDataReceived += (s, e) =>
            {
                if (e.Data is null || !e.Data.EndsWith("\" }, "))
                    return;

                var line = e.Data.AsSpan(0, e.Data.Length - 5); // the length of ' }, 'at end of line

                for (int i = 0; i < selectedFiles.Count; i++)
                {
                    if (!line.EndsWith(selectedFiles[i].File.AsSpan()))
                        continue;

                    int argStart = line.IndexOf('[') + 1;
                    Span<char> command = stackalloc char[line.Length - argStart];
                    line.Slice(argStart).CopyTo(command);

                    //               ], "output" : "
                    var outputArg = "            -Fo".AsSpan();
                    //             "file" : "
                    var fileArg = "          ".AsSpan();

                    outputArg.CopyTo(command.Slice(command.LastIndexOf(']'), outputArg.Length));
                    fileArg.CopyTo(command.Slice(command.LastIndexOf("\"file\" : \"".AsSpan()), fileArg.Length));

                    for (int j = 0; j < command.Length; j++)
                    {
                        if (command[j] == '\\')
                            j++;
                        else if (command[j] == '"' || command[j] == ',')
                            command[j] = ' ';
                    }

                    TaskItem commandItem = new(command.ToString());
                    commandItem.SetMetadata("File", SelectedFiles[selectedFiles[i].Index]);
                    commands.Add(commandItem);
                    selectedFiles.RemoveAt(i);

                    if (selectedFiles.Count == 0)
                        jamBuild.Kill();

                    return;
                }
            };

            jamBuild.Start();
            jamBuild.BeginOutputReadLine();
            jamBuild.WaitForExit();
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
