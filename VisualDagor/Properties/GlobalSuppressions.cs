// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// In debug build every method check for the main thread, but in release build only those check for thr main thread which are called outside of this library.
#if RELEASE
[assembly: SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "<Pending>", Scope = "member", Target = "~M:Gaijin.VisualDagor.MSBuildHelper.GetProject(Microsoft.VisualStudio.Shell.Interop.IVsHierarchy)~EnvDTE.Project")]
[assembly: SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "<Pending>", Scope = "member", Target = "~M:Gaijin.VisualDagor.StartupProjectWatcher.GetProjects(System.Boolean)~System.Collections.Generic.IEnumerable{Microsoft.VisualStudio.Shell.Interop.IVsHierarchy}")]
[assembly: SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "<Pending>", Scope = "member", Target = "~M:Gaijin.VisualDagor.StartupProjectWatcher.Dispose")]
#endif