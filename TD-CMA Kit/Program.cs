using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TD_CMAKit
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Input the file path");
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
            Compiler compiler = new(codes.ToArray());

            (string[] asm, Dictionary<string, string> ist) = compiler.Compile();
            foreach (string a in asm)
            {
                Console.WriteLine(a);
            }

            Console.WriteLine();

            foreach ((string key, string value) in ist)
            {
                Console.WriteLine($"{key}: {value}");
            }

            Console.WriteLine();

            foreach (string a in asm)
            {
                Console.WriteLine(Assembler.Translate(a));
            }
        }
    }
}
