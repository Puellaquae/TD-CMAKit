using System;
using System.Collections.Generic;
using System.IO;
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

            (string[] asm, Dictionary<string, List<(string opcode, int bitLen)>> ist, CodeNode codeGraph) = compiler.Compile();
            foreach (string a in asm)
            {
                Console.WriteLine(a);
            }

            Console.WriteLine();

            foreach ((string key, List<(string opcode, int bitLen)> value) in ist)
            {
                int idx = 0;
                foreach (var (opcode, bitLen) in value)
                {
                    Console.WriteLine($"{key}: Mode {idx++} {opcode}, {bitLen} bits.");
                }
            }

            Console.WriteLine();

            foreach (string a in asm)
            {
                Console.WriteLine(MicrocodeAssembler.Translate(a));
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
            string[] hex = assembler.Assemble(acodes.ToArray());
            foreach (string l in hex)
            {
                Console.WriteLine(l);
            }
        }
    }
}
