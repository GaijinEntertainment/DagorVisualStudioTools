using System;

namespace Gaijin.VisualDagor;

internal static class Constants
{
    public static readonly Guid PackageGuid = new("{4c3c1106-0718-453f-9c9a-1a2c60f725c0}");

    public const string PackageGuidString = "4c3c1106-0718-453f-9c9a-1a2c60f725c0";
    public const string DagorProjectIdentifier = "DagorProject";
    public const string DagorSharedProjectIdentifier = "DagorSharedProject";
    public const string RuleIdentifier = "SharedProject";
    public const string WorkingDirProperty = "ProjectRootDir";
    public const string CommandLineProperty = "JamBuildCommandLine";
    public const string PreprocessorProperty = "NMakePreprocessorDefinitions";
}
