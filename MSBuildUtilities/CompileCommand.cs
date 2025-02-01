using System.Collections.Generic;
using Newtonsoft.Json;

namespace Gaijin.MSBuild.Utilities;

internal sealed class CompileCommand
{
    [JsonProperty("directory")]
    public string Directory { get; set; }

    [JsonProperty("file")]
    public string File { get; set; }

    [JsonProperty("arguments")]
    public List<string> Arguments { get; set; }

    [JsonProperty("output")]
    public string Output { get; set; }
}
