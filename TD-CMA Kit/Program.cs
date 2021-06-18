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
            using StreamReader reader = new(Console.ReadLine().Trim('"'));
            List<string> codes = new();
            string line;
            while ((line = reader.ReadLine()) is not null)
            {
                line = line.Trim();
                if (line != "")
                {
                    codes.Add(line);
                }
            }
            MicrocodeCompiler compiler = new(codes.ToArray());

            (string[] asm, Dictionary<string, InstructionInf> ist) = compiler.Compile();
            foreach (string a in asm)
            {
                Console.WriteLine(a);
            }

            Console.WriteLine();

            foreach ((string key, InstructionInf value) in ist)
            {
                Console.WriteLine($"{key}: {value.OpCode}, {value.BitLen} bits, {value.Additional}");
            }

            Console.WriteLine();

            foreach (string a in asm)
            {
                Console.WriteLine(MicrocodeAssembler.Translate(a));
            }
        }
    }
}
