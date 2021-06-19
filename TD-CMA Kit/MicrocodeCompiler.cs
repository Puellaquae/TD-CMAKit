using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TD_CMAKit
{
    public class MicrocodeCompiler
    {
        private enum LEFT
        {
            PC,
            MEM,
            AR,
            IR,
            OUT,
            RS,
            RD,
            A,
            B
        }

        private enum RIGHT
        {
            PC,
            MEM,
            IN,
            RS,
            RD,
            A,
            B,
            A_AND_B,
            A_OR_B,
            NOT_A,
            A_ROR_B,
            A_SR1,
            A_SL1,
            A_ADD_B,
            A_ADC_B,
            A_SUB_B,
            A_DEC,
            A_INC
        }

        private readonly string[] codes;
        private readonly Dictionary<int, string> asmCodes = new();
        private readonly Dictionary<string, int> labelTable = new();
        private readonly Dictionary<string, int> labelInRealTable = new();

        public class InstructionInf
        {
            public string OpCode { get; set; } = "XXXXXXXX";
            public int BitLen { get; set; } = 1;
            public string Additional { get; set; } = "";

        }

        private Dictionary<string, InstructionInf> instructionSet = new();
        private string currentIST = null;
        private static LEFT LeftParse(string left)
        {
            _ = Enum.TryParse(left, out LEFT res);
            return res;
        }

        private static RIGHT RightParse(string right)
        {
            return right switch
            {
                "A&B" => RIGHT.A_AND_B,
                "A|B" => RIGHT.A_OR_B,
                "~A" => RIGHT.NOT_A,
                "A<<<B" => RIGHT.A_ROR_B,
                "A<<1" => RIGHT.A_SL1,
                "A>>1" => RIGHT.A_SR1,
                "A+B" => RIGHT.A_ADD_B,
                "A+B+CN" => RIGHT.A_ADC_B,
                "A-B" => RIGHT.A_SUB_B,
                "A+1" => RIGHT.A_INC,
                "A-1" => RIGHT.A_DEC,
                _ => (RIGHT)Enum.Parse(typeof(RIGHT), right)
            };
        }

        public MicrocodeCompiler(string[] codes)
        {
            this.codes = codes;
        }

        public string TransAssign(int place, string left, string right, int next, ref int bitLen, string test = null)
        {
            LEFT l = LeftParse(left);
            RIGHT r = RightParse(right);

            if (r == RIGHT.PC)
            {
                bitLen++;
            }

            if (currentIST != null)
            {
                instructionSet[currentIST].BitLen = bitLen;

                if (l == LEFT.RS || r == RIGHT.RS)
                {
                    string ist = instructionSet[currentIST].OpCode;
                    if (ist[4..6] == "XX" || ist[4..6] == "RS")
                    {
                        instructionSet[currentIST].OpCode = ist[0..4] + "RS" + ist[6..];
                    }
                    else
                    {
                        instructionSet[currentIST].Additional += "I4I5 Should Reserve for RS";
                    }
                }
                if (l == LEFT.RD || r == RIGHT.RD)
                {
                    string ist = instructionSet[currentIST].OpCode;
                    if (ist[6..8] == "XX" || ist[6..8] == "RD")
                    {
                        instructionSet[currentIST].OpCode = ist[0..6] + "RD" + ist[8..];
                    }
                    else
                    {
                        instructionSet[currentIST].Additional += "I6I7 Should Reserve for RD";
                    }
                }
                if (r == RIGHT.IN)
                {
                    instructionSet[currentIST].Additional += "Use In Need AR 0b00XXXXXX. ";
                }
                if (l == LEFT.OUT)
                {
                    instructionSet[currentIST].Additional += "Use Out Need AR 0b01XXXXXX. ";
                }
            }
            return test is null ? $"{place:X2} {GetSign(l)} {GetSign(r)} {next:X2}" : $"{place:X2} {GetSign(l)} {GetSign(r)} {test} {next:X2}";
        }

        public static string Nop(int place, int next, string test = null)
        {
            return test is null ? $"{place:X2} {next:X2}" : $"{place:X2} {test ?? ""} {next:X2}";
        }

        private static string GetSign(LEFT left)
        {
            return left switch
            {
                LEFT.PC => "LOAD LDPC",
                LEFT.MEM => "WR",
                LEFT.AR => "LDAR",
                LEFT.IR => "LDIR",
                LEFT.OUT => "WR IOM",
                LEFT.RS => "LDRi",
                LEFT.RD => "LDRi",
                LEFT.A => "LDA",
                LEFT.B => "LDB",
                _ => throw new NotImplementedException(),
            };
        }

        private static string GetSign(RIGHT right)
        {
            return right switch
            {
                RIGHT.PC => "PC_B LDPC",
                RIGHT.MEM => "RD",
                RIGHT.IN => "RD IOM",
                RIGHT.RS => "RS_B",
                RIGHT.RD => "RD_B",
                RIGHT.A => "A ALU_B",
                RIGHT.B => "B ALU_B",
                RIGHT.A_AND_B => "AND ALU_B",
                RIGHT.A_OR_B => "OR ALU_B",
                RIGHT.NOT_A => "NOT ALU_B",
                RIGHT.A_ROR_B => "ROR ALU_B",
                RIGHT.A_SR1 => "SR ALU_B",
                RIGHT.A_SL1 => "SL ALU_B",
                RIGHT.A_ADD_B => "ADD ALU_B",
                RIGHT.A_ADC_B => "ADC ALU_B",
                RIGHT.A_SUB_B => "SUB ALU_B",
                RIGHT.A_DEC => "DEC ALU_B",
                RIGHT.A_INC => "INC ALU_B",
                _ => throw new NotImplementedException(),
            };
        }

        private int GetNextAvailableIndex()
        {
            int i = 0;

            for (; asmCodes.ContainsKey(i); i++)
            {
            }

            return i;
        }

        /// <summary>
        /// 将微指令汇编转换为伪微指令并生成指令推测信息
        /// </summary>
        /// <exception cref="UnknownTokenException"></exception>
        /// <returns></returns>
        public (string[] asmCodes, Dictionary<string, InstructionInf> instructionSetHint) Compile()
        {
            BuildLabelTable();
            ReservePlaceForTestBranch();

            CompileLabel("START", GetNextAvailableIndex());

            string[] asm = (from kp in asmCodes where kp.Value != "Reserve" orderby kp.Key select kp.Value).ToArray();
            return (asm, instructionSet);
        }

        private void CompileLabel(string label, int placeHint, int offset = 0, int fromTest = 0, string certainOpcode = "XXXXXXXX", int bitLen = 0)
        {
            int idx = labelTable[label];
            bool isInstruct = codes[idx - 1].Trim(',', ':').EndsWith('#');
            certainOpcode = CalcCertainOpcode(certainOpcode, offset, fromTest);
            if (isInstruct)
            {
                if (!instructionSet.ContainsKey(label))
                {
                    instructionSet.Add(label, new InstructionInf());
                }

                instructionSet[label].OpCode = certainOpcode;
                instructionSet[label].BitLen = bitLen;
                currentIST = label;
            }

            int place = placeHint + offset;
            labelInRealTable[label] = place;

            if (codes[idx].StartsWith("GOTO"))
            {
                string nextLabel = codes[idx].Split(' ')[1];
                int next = labelInRealTable[nextLabel];
                Nop(place, next);

                currentIST = null;
                return;
            }

            for (; idx < codes.Length; idx++)
            {
                // 跳过标号
                string code = codes[idx];
                if (code.StartsWith('.'))
                {
                    continue;
                }

                // 这里下一行会影响指令的跳转和决定块处理的结束，要前看一行
                string nextLine = codes[idx + 1];
                if (nextLine.StartsWith("<P"))
                {
                    string[] tests = codes[idx + 1].Trim('<', '>').Split(':');
                    int next = int.Parse(tests[1], NumberStyles.HexNumber);
                    CompileAssign(idx, place, next, ref bitLen, tests[0]);
                    if (tests[0] == "P1")
                    {
                        CompileTestP1(idx + 2, next, certainOpcode, bitLen);
                    }
                    else if (tests[0] == "P2")
                    {
                        CompileTestP2(idx + 2, next, certainOpcode, bitLen);
                    }
                    else if (tests[0] == "P3")
                    {
                        CompileTestP3(idx + 2, next, certainOpcode, bitLen);
                    }
                    else
                    {
                        throw new UnknownTokenException($"Unknown Token {tests[0]} in {nextLine}");
                    }

                    break;
                }
                else if (nextLine.StartsWith("GOTO"))
                {
                    string nextLabel = nextLine.Split(' ')[1];
                    int next = labelInRealTable[nextLabel];
                    CompileAssign(idx, place, next, ref bitLen);

                    break;
                }

                place = CompileAssign(idx, place, ref bitLen);
            }

            // 跳出当前块一律视为指令的结束
            currentIST = null;
        }

        private static string CalcCertainOpcode(string certainOpcode, int offset, int fromTest)
        {
            char[] ist = certainOpcode.ToCharArray();
            if (fromTest == 1)
            {
                if (offset >= 0b1100)
                {
                    ist[7 - 7] = '1';
                    ist[7 - 6] = '1';
                    string i1i0 = Convert.ToString(offset & 0b11, 2).PadLeft(2, '0');
                    ist[7 - 1] = i1i0[1 - 1];
                    ist[7 - 0] = i1i0[1 - 0];
                }
                else
                {
                    string i7i6i5i4 = Convert.ToString(offset & 0b1111, 2).PadLeft(4, '0');
                    ist[7 - 7] = i7i6i5i4[3 - 3];
                    ist[7 - 6] = i7i6i5i4[3 - 2];
                    ist[7 - 5] = i7i6i5i4[3 - 1];
                    ist[7 - 4] = i7i6i5i4[3 - 0];
                }
            }
            else if (fromTest == 2)
            {
                string i5i4 = Convert.ToString(offset & 0b11, 2).PadLeft(2, '0');
                ist[7 - 5] = i5i4[1 - 1];
                ist[7 - 4] = i5i4[1 - 0];
            }

            return new string(ist);
        }

        private void CompileTestP3(int idx, int basePlace, string certainOpcode, int bitLen)
        {
            basePlace &= 0b101111;
            for (; idx < codes.Length; idx++)
            {
                string[] tokens = codes[idx].Trim(':').Split(':');
                if (tokens.Length != 2)
                {
                    return;
                }
                int offset = int.Parse(tokens[0], NumberStyles.HexNumber);
                offset &= 0b10000;
                CompileLabel(tokens[1], basePlace, offset, 3, certainOpcode, bitLen);
            }
        }

        private void CompileTestP2(int idx, int basePlace, string certainOpcode, int bitLen)
        {
            basePlace &= 0b111100;
            for (; idx < codes.Length; idx++)
            {
                string[] tokens = codes[idx].Trim(':').Split(':');
                if (tokens.Length != 2)
                {
                    return;
                }
                int offset = int.Parse(tokens[0], NumberStyles.HexNumber);
                offset &= 0b11;
                CompileLabel(tokens[1], basePlace, offset, 2, certainOpcode, bitLen);
            }
        }

        private void CompileTestP1(int idx, int basePlace, string certainOpcode, int bitLen)
        {
            basePlace &= 0b110000;
            for (; idx < codes.Length; idx++)
            {
                string[] tokens = codes[idx].Trim(':').Split(':');
                if (tokens.Length != 2)
                {
                    return;
                }
                int offset = int.Parse(tokens[0], NumberStyles.HexNumber);
                offset &= 0b1111;
                CompileLabel(tokens[1], basePlace, offset, 1, certainOpcode, bitLen);
            }
        }

        private void CompileAssign(int lineIdx, int placeHint, int nextHint, ref int bitLen, string test = null)
        {
            string code = codes[lineIdx];
            if (code == "NOP")
            {
                asmCodes[placeHint] = Nop(placeHint, nextHint);
                return;
            }
            string[] tokens = code.Split('=');
            string l = tokens[0];
            string r = tokens[1];
            string asm = TransAssign(placeHint, l, r, nextHint, ref bitLen, test);
            asmCodes[placeHint] = asm;
        }

        private int CompileAssign(int lineIdx, int placeHint, ref int bitLen)
        {
            string code = codes[lineIdx];
            asmCodes[placeHint] = "Placeholder";
            int next = GetNextAvailableIndex();
            if (code == "NOP")
            {
                asmCodes[placeHint] = Nop(placeHint, next);
                return next;
            }
            string[] tokens = code.Split('=');
            string l = tokens[0];
            string r = tokens[1];

            string asm = TransAssign(placeHint, l, r, next, ref bitLen);
            asmCodes[placeHint] = asm;
            return next;
        }

        private void BuildLabelTable()
        {
            for (int i = 0; i < codes.Length; i++)
            {
                string code = codes[i];
                if (code.StartsWith('.'))
                {
                    string label = code.Trim('.', ':', '#');
                    labelTable[label] = i + 1;
                    labelInRealTable[label] = -1;
                }
            }
        }

        private void ReservePlaceForTestBranch()
        {
            for (int i = 0; i < codes.Length; i++)
            {
                string code = codes[i];
                if (code.StartsWith("<P"))
                {
                    string[] tests = code.Trim('<', '>').Split(':');
                    int basePlace = int.Parse(tests[1], NumberStyles.HexNumber);
                    if (tests[0] == "P1")
                    {
                        for (int o = 0; o < 0b1111; o++)
                        {
                            asmCodes[basePlace + o] = "Reserve";
                        }
                    }
                    else if (tests[0] == "P2")
                    {
                        asmCodes[basePlace + 0] = "Reserve";
                        asmCodes[basePlace + 1] = "Reserve";
                        asmCodes[basePlace + 2] = "Reserve";
                        asmCodes[basePlace + 3] = "Reserve";

                    }
                    else if (tests[0] == "P3")
                    {
                        asmCodes[basePlace + 0] = "Reserve";
                        asmCodes[basePlace + 0b10000] = "Reserve";
                    }
                }
            }
        }
    }
}
