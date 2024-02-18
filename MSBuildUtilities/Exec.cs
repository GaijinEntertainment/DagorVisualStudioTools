using Microsoft.Build.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Gaijin.MSBuild.Utilities
{
    public class Exec : Microsoft.Build.Tasks.Exec
    {
        public string[] PathtoOmit { get; set; }

        readonly Regex regexPath;
        readonly Regex regexUnit;

        private int targetCount = 0;
        private int targetIndex = 0;
        private int finishedCount = 0;

        public Exec() : base()
        {
            regexPath = new Regex("(^\\.\\.?\\/[^\\n\" ?: *<>|]+\\.[A-z0 - 9]+)(.*)");
            regexUnit = new Regex(@"\.(?:cpp|c|cc|inl|lib|asm|masm|S|das|rc|exe|elf|self|hlsl|pssl|js|html)$");
        }

        private static bool TryParseInt(ReadOnlySpan<char> s, out int result)
        {
            result = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsDigit(s[i]))
                    return false;
                result = result * 10 + (s[i] - '0');
            }
            return true;
        }

        unsafe private static void Append(ref StringBuilder sb, ReadOnlySpan<char> s)
        {
            fixed (char* cp = s)
            {
                sb.Append(cp, s.Length);
            }
        }

        private bool FindPathIndextoOmit(ReadOnlySpan<char> path, out int index)
        {
            index = 0;
            if (PathtoOmit != null)
            {
                for (; index < PathtoOmit.Length; index++)
                {
                    if (path.StartsWith(PathtoOmit[index].AsSpan()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool FindPathIndextoOmit(ref string path, out int index)
        {
            return FindPathIndextoOmit(path.AsSpan(), out index);
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            var line = singleLine.AsSpan();
            for (int i = 0; i < line.Length; i++)
            {
                if (!char.IsControl(line[i]))
                {
                    line = line.Slice(i);
                    break;
                }
            }

            if (line.Length < 3 || line.StartsWith("---".AsSpan()) || line.StartsWith("SUCCESSFULLY built".AsSpan()))
                return;

            if (line[0] == '.')
            {
                if (line[1] == '.')
                {
                    if (line[2] == '.')
                    {
                        var line_ = line.Slice(3);
                        if (line_.StartsWith("patience...".AsSpan()))
                            return;

                        if (line_.StartsWith("on ".AsSpan()))
                        {
                            // The length of "on Xth target..." without the number is 21
                            TryParseInt(line_.Slice(3, length: line_.Length - 15), out int tmpFinishedCount);
                            if (targetIndex < finishedCount + tmpFinishedCount - 1)
                                targetIndex = finishedCount + tmpFinishedCount - 1;
                            return;
                        }

                        if (line_.StartsWith("updated ".AsSpan()))
                        {
                            // The length of "updated X target(s)..." without the number is 21
                            TryParseInt(line_.Slice(8, length: line_.Length - 21), out int tmpFinishedCount);
                            finishedCount += tmpFinishedCount;
                            return;
                        }

                        if (line_.StartsWith("updating ".AsSpan()))
                        {
                            // The length of "updating X target(s)..." without the number is 22
                            TryParseInt(line_.Slice(9, length: line_.Length - 22), out int tmpTargetCount);
                            targetCount += tmpTargetCount;
                        }

                        Log.LogMessageFromText(line_.Slice(0, length: line_.Length - 3).ToString(), messageImportance);
                        return;
                    }

                    if (line[2] == '/')
                    {
                        var groups = regexPath.Split(line.ToString());
                        if (groups.Length == 4)
                        {
                            var fullPath = Path.GetFullPath(Path.Combine(GetWorkingDirectory(), groups[1]));
                            string path = FindPathIndextoOmit(ref fullPath, out int index) ? fullPath.Replace(PathtoOmit[index], "") : fullPath;
                            Log.LogMessageFromText(string.Join("", path, groups[2]), messageImportance);
                            return;
                        }
                    }
                    else if (line[2] == '\\')
                    {
                        int parentIndex = line.IndexOf('(') - 1;
                        if (parentIndex > 0)
                        {
                            var fullPath = Path.GetFullPath(Path.Combine(GetWorkingDirectory(), line.Slice(0, parentIndex).ToString()));
                            string path = FindPathIndextoOmit(ref fullPath, out int index) ? fullPath.Replace(PathtoOmit[index], "") : fullPath;
                            Log.LogMessageFromText(string.Join("", path, line.Slice(parentIndex).ToString()), messageImportance);
                            return;
                        }
                    }

                }
            }

            if ((regexUnit.IsMatch(singleLine) && !line.StartsWith("buildStamp.c".AsSpan())) || line.StartsWith("generated ".AsSpan()))
            {
                var sb = new StringBuilder(16 + line.Length);
                sb.Append('[');
                sb.Append(++targetIndex);
                sb.Append('/');
                sb.Append(Math.Max(targetIndex, targetCount));
                sb.Append("] ");
                Append(ref sb, line);

                Log.LogMessageFromText(sb.ToString(), messageImportance);
                return;
            }

            if (line.StartsWith(@"* copy file to:".AsSpan()))
                ++targetIndex;
            else if (line.StartsWith(" . ".AsSpan()))
                line = line.Slice(3);
            else if (FindPathIndextoOmit(line, out int index))
                line = line.Slice(PathtoOmit[index].Length);

            Log.LogMessageFromText(line.ToString(), messageImportance);
        }

        public static void StopProgram(uint pid)
        {
            // It's impossible to be attached to 2 consoles at the same time,
            // so release the current one.
            NativeMethods.FreeConsole();

            // This does not require the console window to be visible.
            if (NativeMethods.AttachConsole(pid))
            {
                // Disable Ctrl-C handling for our program
                NativeMethods.SetConsoleCtrlHandler(null, true);
                NativeMethods.GenerateConsoleCtrlEvent(NativeMethods.CtrlTypes.CTRL_C_EVENT, 0);

                // Must wait here. If we don't and re-enable Ctrl-C
                // handling below too fast, we might terminate ourselves.
                Thread.Sleep(2000);

                NativeMethods.FreeConsole();

                // Re-enable Ctrl-C handling or any subsequently started
                // programs will inherit the disabled state.
                NativeMethods.SetConsoleCtrlHandler(null, false);
            }
        }

        public override void Cancel()
        {
            ManagementObjectSearcher mos = new ManagementObjectSearcher(
                string.Format("Select * From Win32_Process Where ParentProcessID={0}", Process.GetCurrentProcess().Id));

            foreach (ManagementObject mo in mos.Get().Cast<ManagementObject>())
            {
                var name = Convert.ToString(mo["Name"]);
                if (name == "cmd.exe")
                {
                    int childProcessID = Convert.ToInt32(mo["ProcessID"]);
                    var childProcess = Process.GetProcessById(childProcessID);
                    StopProgram((uint)childProcessID);
                    childProcess.WaitForExit();

                    break;
                }
            }

            base.Cancel();
        }
    }
}
