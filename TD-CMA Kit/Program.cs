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
            using StreamReader mreader = new(Console.ReadLine().Trim('"'));
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
                try
                {
                    Console.WriteLine(MicrocodeAssembler.Translate(a));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            Console.WriteLine("Microcode loading finish");
            Console.WriteLine("Input the assemble file path");

            using StreamReader areader = new(Console.ReadLine().Trim('"'));

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
