using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using static TD_CMAKit.MicrocodeCompiler;
using static TD_CMAKit.Program;

return;

namespace TD_CMAKit
{
    public partial class Program
    {
        static Dictionary<string, List<(string opcode, int bitLen)>> istCache = null;

        [JSExport]
        static string MicroCode(string mcodeStr)
        {
            using StringReader mreader = new(mcodeStr);
            List<string> mcodes = new();
            string line;
            while ((line = mreader.ReadLine()) is not null)
            {
                line = line.Trim();
                if (line != "")
                {
                    mcodes.Add(line);
                }
            }
            MicrocodeCompiler compiler = new(mcodes.ToArray());

            Dictionary<int, string> asm = compiler.Compile();
            Dictionary<string, List<(string opcode, int bitLen)>> ist = compiler.InstructionSetHint;
            CodeNode codeGraph = compiler.CodeNodeGraph;
            Dictionary<int, int> realToRaw = compiler.RealToRawMap;
            string[] pesudoMC = (from p in asm orderby p.Key select p.Value).ToArray();
            List<string> asmHint = new();
            foreach ((string key, List<(string opcode, int bitLen)> value) in ist)
            {
                int idx = 0;
                foreach ((string opcode, int bitLen) in value)
                {
                    asmHint.Add($"{key}: Mode {idx++} {opcode}, {bitLen} bytes.");
                }
            }
            List<string> binMC = new();
            foreach ((int i, string a) in from p in asm orderby p.Key select p)
            {
                string h = MicrocodeAssembler.Translate(a).ToString();
                binMC.Add($"{h} ; {mcodes[realToRaw[i]]}");
            }

            istCache = ist;

            var pmc = string.Join(",", pesudoMC.Select(x => $"\"{x}\""));
            var ah = string.Join(",", asmHint.Select(x => $"\"{x}\""));
            var bmc = string.Join(",", binMC.Select(x => $"\"{x}\""));

            return $"{{\"pmc\":[{pmc}],\"ah\":[{ah}],\"bmc\":[{bmc}]}}";
        }

        [JSExport]
        static byte[] MicroCodeImg(string mcodeStr)
        {
            using StringReader mreader = new(mcodeStr);
            List<string> mcodes = new();
            string line;
            while ((line = mreader.ReadLine()) is not null)
            {
                line = line.Trim();
                if (line != "")
                {
                    mcodes.Add(line);
                }
            }
            MicrocodeCompiler compiler = new(mcodes.ToArray());

            Dictionary<int, string> asm = compiler.Compile();
            Dictionary<string, List<(string opcode, int bitLen)>> ist = compiler.InstructionSetHint;
            CodeNode codeGraph = compiler.CodeNodeGraph;
            Dictionary<int, int> realToRaw = compiler.RealToRawMap;
            string[] pesudoMC = (from p in asm orderby p.Key select p.Value).ToArray();
            List<string> asmHint = new();
            foreach ((string key, List<(string opcode, int bitLen)> value) in ist)
            {
                int idx = 0;
                foreach ((string opcode, int bitLen) in value)
                {
                    asmHint.Add($"{key}: Mode {idx++} {opcode}, {bitLen} bytes.");
                }
            }
            List<string> binMC = new();
            foreach ((int i, string a) in from p in asm orderby p.Key select p)
            {
                string h = MicrocodeAssembler.Translate(a).ToString();
                binMC.Add($"{h} ; {mcodes[realToRaw[i]]}");
            }

            var data = FlowDiagram.GetBlob(codeGraph);

            return data;
        }

        [JSExport]
        static string[] AsmCode(string asmCodeStr, string ists)
        {
            using StringReader areader = new(asmCodeStr);
            string line;
            List<string> acodes = new();
            while ((line = areader.ReadLine()) is not null)
            {
                line = line.Trim();
                if (line != "")
                {
                    acodes.Add(line);
                }
            }
            var ist = istCache;
            Assembler assembler = new(ist);
            Dictionary<int, int> hex2AsmMap = new();
            string[] hex = assembler.Assemble(acodes.ToArray(), hex2AsmMap);
            List<string> res = new();
            for (var index = 0; index < hex.Length; index++)
            {
                string l = hex[index];
                res.Add(hex2AsmMap.ContainsKey(index) ? $"{l} ; {acodes[hex2AsmMap[index]]}" : l);
            }
            return res.ToArray();
        }
    }
}
