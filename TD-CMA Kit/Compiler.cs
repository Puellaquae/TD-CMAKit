using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TD_CMAKit
{
    public class Compiler
    {
        public enum LEFT
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

        public enum RIGHT
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

        private string[] codes;
        private Dictionary<int, string> asmCodes = new Dictionary<int, string>();
        private Dictionary<string, int> labelTable = new Dictionary<string, int>();
        private Dictionary<string, int> labelInRealTable = new Dictionary<string, int>();
        private Dictionary<string, string> instructionSet = new Dictionary<string, string>();
        string currentIST = null;
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

        public Compiler(string[] codes)
        {
            this.codes = codes;
        }

        public string TransAssign(int place, string left, string right, int next, string test = null)
        {
            LEFT l = LeftParse(left);
            RIGHT r = RightParse(right);
            if (currentIST != null)
            {
                if (l == LEFT.RS || r == RIGHT.RS)
                {
                    instructionSet[currentIST] += ", Use RS";
                }
                else if (l == LEFT.RD || r == RIGHT.RD)
                {
                    instructionSet[currentIST] += ", Use RD";
                }
                else if (r == RIGHT.PC)
                {
                    instructionSet[currentIST] += ", Read Next Byte";
                }
                else if (r == RIGHT.IN)
                {
                    instructionSet[currentIST] += ", Use In Need AR 0b01";
                }
                else if (l == LEFT.OUT)
                {
                    instructionSet[currentIST] += ", Use Out Need AR 0b10";
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

        int GetNextAvailableIndex()
        {
            int i = 0;

            for (; asmCodes.ContainsKey(i); i++)
            {
            }

            return i;
        }

        public (string[] asmCodes, Dictionary<string, string> instructionSetHint) Compile()
        {
            BuildLabelTable();

            CompileLabel("START", GetNextAvailableIndex());

            string[] asm = (from kp in asmCodes orderby kp.Key select kp.Value).ToArray();

            return (asm, instructionSet);
        }

        void CompileLabel(string label, int placeHint, int offset = 0, int fromTest = 0, string certainOpcode = "XXXXXXXX")
        {
            int idx = labelTable[label];
            bool isInstruct = codes[idx - 1].Trim(',', ':').EndsWith('#');
            certainOpcode = CalcCertainOpcode(certainOpcode, offset, fromTest);
            if (isInstruct)
            {
                instructionSet[label] = certainOpcode;
                currentIST = label;
            }

            int place = placeHint + offset;
            labelInRealTable[label] = place;
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
                    CompileAssign(idx, place, next, tests[0]);
                    if (tests[0] == "P1")
                    {
                        CompileTestP1(idx + 2, next, certainOpcode);
                    }
                    else if (tests[0] == "P2")
                    {
                        CompileTestP2(idx + 2, next, certainOpcode);
                    }
                    else if (tests[0] == "P3")
                    {
                        CompileTestP3(idx + 2, next, certainOpcode);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    break;
                }
                else if (nextLine.StartsWith("GOTO"))
                {
                    string nextLabel = nextLine.Split(' ')[1];
                    int next = labelInRealTable[nextLabel];
                    CompileAssign(idx, place, next);

                    break;
                }

                place = CompileAssign(idx, place);
            }

            // 跳出当前块一律视为指令的结束
            currentIST = null;
        }

        private string CalcCertainOpcode(string certainOpcode, int offset, int fromTest)
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

        void CompileTestP3(int idx, int basePlace, string certainOpcode)
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
                CompileLabel(tokens[1], basePlace, offset, 3, certainOpcode);
            }
        }

        void CompileTestP2(int idx, int basePlace, string certainOpcode)
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
                CompileLabel(tokens[1], basePlace, offset, 2, certainOpcode);
            }
        }

        void CompileTestP1(int idx, int basePlace, string certainOpcode)
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
                CompileLabel(tokens[1], basePlace, offset, 1, certainOpcode);
            }
        }

        void CompileAssign(int lineIdx, int placeHint, int nextHint, string test = null)
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
            string asm = TransAssign(placeHint, l, r, nextHint, test);
            asmCodes[placeHint] = asm;
        }

        int CompileAssign(int lineIdx, int placeHint, string test = null)
        {
            string code = codes[lineIdx];
            asmCodes[placeHint] = "Placeholder";
            int next = GetNextAvailableIndex();
            if (code == "NOP")
            {

                asmCodes[placeHint] = Nop(placeHint, next, test);
                return next;
            }
            string[] tokens = code.Split('=');
            string l = tokens[0];
            string r = tokens[1];

            string asm = TransAssign(placeHint, l, r, next);
            asmCodes[placeHint] = asm;
            return next;
        }

        void BuildLabelTable()
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
    }
}
