using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static TD_CMAKit.MicrocodeCompiler;

namespace TD_CMAKit
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Input the microcode file path");
            string filepath = Console.ReadLine()!.Trim('"');
            using StreamReader mreader = new(filepath);
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
            foreach ((int _, string a) in from p in asm orderby p.Key select p)
            {
                Console.WriteLine(a);
            }

            Console.WriteLine();

            foreach ((string key, List<(string opcode, int bitLen)> value) in ist)
            {
                int idx = 0;
                foreach ((string opcode, int bitLen) in value)
                {
                    Console.WriteLine($"{key}: Mode {idx++} {opcode}, {bitLen} bytes.");
                }
            }

            Console.WriteLine();

            foreach ((int i, string a) in from p in asm orderby p.Key select p)
            {
                string h = MicrocodeAssembler.Translate(a).ToString();
                Console.WriteLine($"{h} ; {mcodes[realToRaw[i]]}");
            }

            FlowDiagram.Draw(codeGraph, filepath + ".png");

            Console.WriteLine($"Microcode Flow Diagram Save To {filepath}.png");
            Console.WriteLine("Microcode loading finish");
            Console.WriteLine("Input the assemble file path");

            using StreamReader areader = new(Console.ReadLine()!.Trim('"'));

            List<string> acodes = new();
            while ((line = areader.ReadLine()) is not null)
            {
                line = line.Trim();
                if (line != "")
                {
                    acodes.Add(line);
                }
            }

            Assembler assembler = new(ist);
            Dictionary<int, int> hex2AsmMap = new();
            string[] hex = assembler.Assemble(acodes.ToArray(), hex2AsmMap);
            for (var index = 0; index < hex.Length; index++)
            {
                string l = hex[index];
                Console.WriteLine(hex2AsmMap.ContainsKey(index) ? $"{l} ; {acodes[hex2AsmMap[index]]}" : l);
            }
        }
    }
}
