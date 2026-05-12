using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MCTranslator
{
    public class TranslationSession
    {
        public string? PackType { get; set; }        // "resource", "shader", "modpack"
        public string? TempDir { get; set; }
        public List<string> ModpackModIds { get; set; } = new();
        public Dictionary<string, string> SourceDict { get; set; } = new();
        public Dictionary<string, string> TargetDict { get; set; } = new();
        public int ErrorCount { get; set; } = 0;
        public Dictionary<string, string> CfpaDict { get; set; } = new();   // 未使用但保留
        public string? FilePathOrFolder { get; set; } // 记录当前选择的文件/文件夹路径

        public CancellationTokenSource? Cts { get; set; }

        public bool HasSource => SourceDict.Count > 0;
        public bool HasTarget => TargetDict.Count > 0;

        public void Clear()
        {
            PackType = null;
            TempDir = null;
            ModpackModIds.Clear();
            SourceDict.Clear();
            TargetDict.Clear();
            ErrorCount = 0;
            CfpaDict.Clear();
            FilePathOrFolder = null;
            Cts?.Cancel();
            Cts = null;
        }
    }
}