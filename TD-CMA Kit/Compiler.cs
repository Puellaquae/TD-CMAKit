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

        void CompileLabel(string label, int placeHint)
        {
            int idx = labelTable[label];
            int place = placeHint;
            labelInRealTable[label] = place;
            for (; idx < codes.Length; idx++)
            {
                string code = codes[idx];
                if (codes[idx + 1].StartsWith("<P"))
                {
                    string[] tests = codes[idx + 1].Trim('<', '>').Split(':');
                    int next = int.Parse(tests[1], NumberStyles.HexNumber);
                    CompileAssign(idx, place, next, tests[0]);
                    if (tests[0] == "P1")
                    {
                        CompileTestP1(idx + 2, next);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    return;
                }

                place = CompileAssign(idx, place);
            }
        }

        void CompileTestP1(int idx, int basePlace)
        {
            for (; idx < codes.Length; idx++)
            {
                string[] tokens = codes[idx].Trim(':').Split(':');
                if (tokens.Length != 2)
                {
                    return;
                }

                CompileInstruct(tokens[1], basePlace, int.Parse(tokens[0], NumberStyles.HexNumber));
            }
        }

        void CompileInstruct(string label, int basePlace, int offset)
        {
            string ist;
            if (offset <= 0b1011)
            {

                ist = $"{Convert.ToString(offset & 0b1111, 2).PadLeft(4, '0')}XXXX";
            }
            else
            {
                ist = $"11XXXX{Convert.ToString(offset & 0b11, 2).PadLeft(2, '0')}";
            }

            instructionSet[label] = ist;
            currentIST = label;
            int idx = labelTable[label];
            int place = basePlace + offset;
            labelInRealTable[label] = place;
            for (; idx < codes.Length; idx++)
            {
                if (codes[idx + 1].StartsWith("GOTO"))
                {
                    string nextLabel = codes[idx + 1].Split(' ')[1];
                    int next = labelInRealTable[nextLabel];
                    CompileAssign(idx, place, next);
                    currentIST = null;
                    return;
                }

                place = CompileAssign(idx, place);
            }
            currentIST = null;
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
                    labelTable[code.Trim('.', ':')] = i + 1;
                    labelInRealTable[code.Trim('.', ':')] = -1;
                }
            }
        }
    }
}
