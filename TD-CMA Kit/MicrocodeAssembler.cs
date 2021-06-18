using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace TD_CMAKit
{
    public class MicrocodeAssembler
    {
        public enum High
        {
            M23 = 0b10000,
            CN = 0b01000,
            WR = 0b00100,
            RD = 0b00010,
            IOM = 0b00001
        }
        public enum A
        {
            NOP = 0,
            LDA = 1,
            LDB = 2,
            LDRi = 3,
            LDSP = 4,
            LOAD = 5,
            LDAR = 6,
            LDIR = 7
        }

        public enum B
        {
            NOP = 0,
            ALU_B = 1,
            RS_B = 2,
            RD_B = 3,
            RI_B = 4,
            SP_B = 5,
            PC_B = 6
        }

        public enum C
        {
            NOP = 0,
            P1 = 1,
            P2 = 2,
            P3 = 3,
            P4 = 4,
            LDPC = 5,
            STI = 8,
            CLI = 7
        }

        public enum ALU
        {
            A = 0b0000,
            B = 0b0001,
            AND = 0b0010,
            OR = 0b0011,
            NOT = 0b0100,
            ROR = 0b0101,
            SR = 0b0110,
            SL = 0b0111,
            CN = 0b1000,
            ADD = 0b1001,
            ADC = 0b1010,
            SUB = 0b1011,
            DEC = 0b1100,
            INC = 0b1101
        }

        public record Instruct
        {
            public int Place { get; set; }
            public int High { get; set; }
            public ALU S { get; set; } = ALU.A;
            public A FieldA { get; set; } = A.NOP;
            public B FieldB { get; set; } = B.NOP;
            public C FieldC { get; set; } = C.NOP;
            public int Address { get; set; }

            public override string ToString()
            {
                int i = ((High & 0b11111) << 19) +
                        (((int)S & 0b1111) << 15) +
                        (((int)FieldA & 0b111) << 12) +
                        (((int)FieldB & 0b111) << 9) +
                        (((int)FieldC & 0b111) << 6) +
                        (Address & 0b111111);
                return $"$M {Place:X2} {i:X6}";
            }

        };

        public static Instruct Translate(String op)
        {
            Instruct instruct = new();
            String[] tokens = op.Split(' ', '\t');
            instruct.Place = int.Parse(tokens[0], NumberStyles.HexNumber);
            for (int i = 1; i < tokens.Length - 1; i++)
            {
                if (Enum.TryParse(tokens[i], out High h))
                {
                    instruct.High |= (int)h;
                }
                else if (Enum.TryParse(tokens[i], out A a))
                {
                    instruct.FieldA = a;
                }
                else if (Enum.TryParse(tokens[i], out B b))
                {
                    instruct.FieldB = b;
                }
                else if (Enum.TryParse(tokens[i], out C c))
                {
                    instruct.FieldC = c;
                }
                else if (Enum.TryParse(tokens[i], out ALU alu))
                {
                    instruct.S = alu;
                }
                else if (tokens[i] == "")
                {
                    continue;
                }
                else
                {
                    throw new Exception($"Unknown Sign {tokens[i]}");
                }

            }

            instruct.Address = int.Parse(tokens[^1], NumberStyles.HexNumber);
            return instruct;
        }

    }
}
