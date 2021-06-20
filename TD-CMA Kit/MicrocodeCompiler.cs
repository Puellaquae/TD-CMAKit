using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Text.Encodings.Web;

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
            RD,
            SP,
            A,
            B
        }

        private enum RIGHT
        {
            PC,
            PC_INC,
            MEM,
            IN,
            RS,
            RD,
            RI,
            SP,
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
        private readonly HashSet<string> compiledLabel = new();
        private string nextInstructionLabel;

        public class CodeNode
        {

            internal int PlaceInReal { get; set; }

            internal string StartOfLabel { get; set; }

            /// <summary>
            /// 记录进入代码当前所确定的 Opcode
            /// 只由 CompileTest 改写 
            /// </summary>
            internal string Opcode { get; set; }

            /// <summary>
            /// 记录是否经过一个测试
            /// 只由 CompileTest 改写 
            /// </summary>
            internal int HasTest { get; set; }

            internal string Code { get; set; }

            internal List<string> ForInstruction { get; set; } = new();

            internal bool NotProcessNext { get; set; }

            internal List<CodeNode> NextNodes { get; } = new();
        }

        private readonly Dictionary<string, CodeNode> labelInCodeGraph = new();

        private readonly Dictionary<string, List<(string opcode, int bitLen)>> instructionSet = new();

        private static LEFT LeftParse(string left)
        {
            return left switch
            {
                "RS" => throw new SyntaxException("RS register is ReadOnly."),
                _ => (LEFT)Enum.Parse(typeof(LEFT), left)
            };
        }

        private static RIGHT RightParse(string right)
        {
            return right switch
            {
                "PC++" => RIGHT.PC_INC,
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

        public static string TransAssign(int place, string left, string right, int next, string test = null)
        {
            LEFT l = LeftParse(left);
            RIGHT r = RightParse(right);

            return test is null ? $"{place:X2} {GetSign(l)} {GetSign(r)} {next:X2}" : $"{place:X2} {GetSign(l)} {GetSign(r)} {test} {next:X2}";
        }

        private static string TransNotAssign(string code, int place, int next, string test = null)
        {
            return test is null ? $"{place:X2} {code} {next:X2}" : $"{place:X2} {code} {test} {next:X2}";
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
                LEFT.SP => "LDSP",
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
                RIGHT.PC => "PC_B",
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
                RIGHT.RI => "RI_B",
                RIGHT.SP => "SP_B",
                RIGHT.PC_INC => "PC_B LDPC",
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
        /// <exception cref="SyntaxException"></exception>
        /// <returns></returns>
        public (string[] asmCodes, Dictionary<string, List<(string opcode, int bitLen)>> instructionSetHint, CodeNode codeGraph) Compile()
        {
            BuildLabelTable();
            ReservePlaceForTestBranch();

            CodeNode startNode = new();
            CompileLabel("START", GetNextAvailableIndex(), startNode);
            BuildInstructionInf(startNode);

            string[] asm = (from kp in asmCodes where kp.Value != "Reserve" orderby kp.Key select kp.Value).ToArray();

            foreach (var (key, value) in instructionSet)
            {
                instructionSet[key] = (from mode in value orderby mode.opcode select mode).ToList();
            }

            return (asm, instructionSet, startNode);
        }

        private void BuildInstructionInf(CodeNode codeNode)
        {
            DetermineInstruction(codeNode);
            DetermineInstructionCode(codeNode, "XXXXXXXX", 0);
        }

        private void DetermineInstructionCode(CodeNode codeNode, string opcode, int bitLen)
        {
            opcode = UnionCertainOpcode(opcode, codeNode.Opcode);
            (opcode, bitLen) = CalcInstructionOpcode(codeNode, opcode, bitLen);

            if (codeNode.NotProcessNext)
            {
                foreach (string instruction in codeNode.ForInstruction)
                {
                    if (!instructionSet.ContainsKey(instruction))
                    {
                        instructionSet.Add(instruction, new List<(string opcode, int bitLen)>());
                    }

                    instructionSet[instruction].Add((opcode, bitLen));
                    instructionSet[instruction] = instructionSet[instruction].Distinct().ToList();
                }
                return;
            }

            foreach (CodeNode nextNode in codeNode.NextNodes)
            {
                DetermineInstructionCode(nextNode, opcode, bitLen);
            }
        }

        private string UnionCertainOpcode(string opcode1, string opcode2)
        {
            if (opcode1 is null && opcode2 is null)
            {
                return "XXXXXXXX";
            }

            if (opcode2 is null)
            {
                return opcode1;
            }

            if (opcode1 is null)
            {
                return opcode2;
            }

            char[] opcode = opcode1.ToCharArray();

            for (int i = 0; i < opcode1.Length; i++)
            {
                if (opcode1[i] == 'X' && opcode2[i] != 'X')
                {
                    opcode[i] = opcode2[i];
                }
            }

            return new string(opcode);
        }

        private void GetAffectedInstruction(CodeNode codeNode, List<string> instructions)
        {
            if (codeNode.NotProcessNext)
            {
                return;
            }

            instructions.AddRange(codeNode.ForInstruction);

            instructions = instructions.Distinct().ToList();

            foreach (CodeNode nextNode in codeNode.NextNodes)
            {
                GetAffectedInstruction(nextNode, instructions);
            }
        }

        private (string opcode, int bitLen) CalcInstructionOpcode(CodeNode codeNode, string opcode, int bitLen)
        {
            string code = codeNode.Code;

            if (!code.Contains('='))
            {
                return (opcode, bitLen);
            }

            string[] tokens = code.Split('=');
            LEFT l = LeftParse(tokens[0].Trim());
            RIGHT r = RightParse(tokens[1].Trim());

            List<string> ists = new();
            GetAffectedInstruction(codeNode, ists);

            if (r == RIGHT.PC_INC)
            {
                bitLen++;
            }

            if (r == RIGHT.RS)
            {
                if (opcode[4..6] == "XX" || opcode[4..6] == "RS")
                {
                    opcode = opcode[..4] + "RS" + opcode[6..];
                }
                else
                {
                    throw new SyntaxException($"I4I5 Should Reserve for RS. In {string.Join(',', ists)}");
                }
            }

            if (l == LEFT.RD || r == RIGHT.RD)
            {
                if (opcode[6..8] == "XX" || opcode[6..8] == "RD")
                {
                    opcode = opcode[..6] + "RD" + opcode[8..];
                }
                else
                {
                    throw new SyntaxException($"I6I7 Should Reserve for RD. In {string.Join(',', ists)}");
                }
            }

            return (opcode, bitLen);
        }

        private void DetermineInstruction(CodeNode codeNode, List<string> instructions = null)
        {
            if (instructions is not null)
            {
                codeNode.ForInstruction.AddRange(instructions);
                codeNode.ForInstruction = codeNode.ForInstruction.Distinct().ToList();
            }

            if (codeNode.NotProcessNext)
            {
                return;
            }

            List<string> ists = codeNode.ForInstruction;
            foreach (CodeNode nextNode in codeNode.NextNodes)
            {
                DetermineInstruction(nextNode, ists);
            }
        }

        private void CompileLabel(string label, int place, CodeNode codeNode)
        {
            if (!labelTable.ContainsKey(label))
            {
                throw new SyntaxException($"{label} is not a label");
            }

            int idx = labelTable[label];

            if (compiledLabel.Contains(label))
            {
                return;
            }

            compiledLabel.Add(label);
            codeNode.StartOfLabel = label;

            // 如果进入一条指令，先标记第一个 Node 
            if (IsInstruct(label))
            {
                codeNode.ForInstruction.Add(label);
            }

            labelInRealTable[label] = place;
            codeNode.PlaceInReal = place;
            labelInCodeGraph[label] = codeNode;

            string code = codes[idx];

            // 简化处理，END、GOTO 和 TEST 不能独立存在
            if (code.StartsWith("END") || code.StartsWith("GOTO") || code.StartsWith("<P"))
            {
                throw new SyntaxException($"END, GOTO and TEST must attached to other code. In {code}");
            }

            for (; idx < codes.Length; idx++)
            {
                // 如果是标号，认为当前块结束，进入下一块
                if (code.StartsWith('.'))
                {
                    string nextLabel = code.Trim('.', ':', '#');
                    CompileLabel(nextLabel, place, codeNode);
                    break;
                }

                // 这里下一行会影响指令的跳转和决定块处理的结束，要前看一行
                string nextLine = codes[idx + 1];
                if (nextLine.StartsWith("<P"))
                {
                    string[] tests = codes[idx + 1].Trim('<', '>').Split(':');
                    int next = Convert.ToInt32(tests[1], 16);

                    CompileLine(idx, place, next, codeNode, tests[0]);
                    CompileTest(codeNode, idx + 2, tests[0], next);

                    break;
                }

                if (nextLine.StartsWith("GOTO"))
                {
                    string nextLabel = nextLine.Split(' ')[1].Trim();

                    if (!labelInRealTable.ContainsKey(nextLabel))
                    {
                        // 如果要跳转的块还未编译，预留位置先去编译
                        asmCodes[place] = "Reserve";
                        int labelPlace = GetNextAvailableIndex();
                        CompileLabel(nextLabel, labelPlace, new CodeNode());
                    }

                    int next = labelInRealTable[nextLabel];
                    codeNode.NextNodes.Add(labelInCodeGraph[nextLabel]);
                    CompileLine(idx, place, next, codeNode);

                    break;
                }

                if (nextLine.StartsWith("END"))
                {
                    string[] tokens = nextLine.Split(' ');
                    string nextLabel;
                    if (tokens.Length == 1)
                    {
                        nextLabel = nextInstructionLabel;
                    }
                    else if (tokens[1].Trim() == label)
                    {
                        nextLabel = label;
                    }
                    else
                    {
                        throw new SyntaxException($"END only can goto Next instruction label or Self label. In {code}");
                    }

                    int next = labelInRealTable[nextLabel];
                    codeNode.NextNodes.Add(labelInCodeGraph[nextLabel]);
                    codeNode.NotProcessNext = true;
                    CompileLine(idx, place, next, codeNode);

                    break;
                }

                place = CompileLine(idx, place, codeNode);
                CodeNode nextLineNode = new();
                codeNode.NextNodes.Add(nextLineNode);
                codeNode = nextLineNode;
            }
        }

        private bool IsInstruct(string label)
        {
            return codes[labelTable[label] - 1].Trim(',', ':').EndsWith('#');
        }

        private void CompileTest(CodeNode codeNode, int idx, string test, int next)
        {
            switch (test)
            {
                case "P1":
                    CompileTestP1(idx, next, codeNode);
                    break;
                case "P2":
                    CompileTestP2(idx, next, codeNode);
                    break;
                case "P3":
                    CompileTestP3(idx, next, codeNode);
                    break;
                case "P4":
                    CompileTestP4(idx, next, codeNode);
                    break;
                default:
                    throw new UnknownTokenException($"Unknown Token {test}. In {codes[idx - 1]}");
            }
        }

        private static string CalcCertainOpcode(int offset, int fromTest)
        {
            char[] ist = "XXXXXXXX".ToCharArray();
            switch (fromTest)
            {
                case 1 when offset >= 0b1100:
                    {
                        ist[7 - 7] = '1';
                        ist[7 - 6] = '1';
                        string i3i2 = Convert.ToString(offset & 0b11, 2).PadLeft(2, '0');
                        ist[7 - 3] = i3i2[1 - 1];
                        ist[7 - 2] = i3i2[1 - 0];
                        break;
                    }
                case 1:
                    {
                        string i7i6i5i4 = Convert.ToString(offset & 0b1111, 2).PadLeft(4, '0');
                        ist[7 - 7] = i7i6i5i4[3 - 3];
                        ist[7 - 6] = i7i6i5i4[3 - 2];
                        ist[7 - 5] = i7i6i5i4[3 - 1];
                        ist[7 - 4] = i7i6i5i4[3 - 0];
                        break;
                    }
                case 2:
                    {
                        string i5i4 = Convert.ToString(offset & 0b11, 2).PadLeft(2, '0');
                        ist[7 - 5] = i5i4[1 - 1];
                        ist[7 - 4] = i5i4[1 - 0];
                        break;
                    }
            }

            return new string(ist);
        }

        private void CompileTestP4(int idx, int basePlace, CodeNode codeNode)
        {
            basePlace &= 0b011111;
            for (; idx < codes.Length; idx++)
            {
                string code = codes[idx];
                string[] tokens = code.Trim(':').Split(':');
                if (tokens.Length != 2)
                {
                    return;
                }

                string branch = tokens[0].Trim();
                string label = tokens[1].Trim();
                if (branch != "Y" && tokens[0] != "N")
                {
                    throw new SyntaxException($"P4 test is a if-else branch, require 'N' or 'Y' rather {branch}. In {code}");
                }

                int offset = branch is "N" ? 0 : 0b100000;
                CodeNode branchNode = new()
                {
                    Opcode = CalcCertainOpcode(offset, 4),
                };
                codeNode.NextNodes.Add(branchNode);
                codeNode.HasTest = 4;
                CompileLabel(label, basePlace + offset, branchNode);
            }
        }

        private void CompileTestP3(int idx, int basePlace, CodeNode codeNode)
        {
            basePlace &= 0b101111;
            for (; idx < codes.Length; idx++)
            {
                string code = codes[idx];
                string[] tokens = code.Trim(':').Split(':');
                if (tokens.Length != 2)
                {
                    return;
                }

                string branch = tokens[0].Trim();
                string label = tokens[1].Trim();
                if (branch != "Y" && tokens[0] != "N")
                {
                    throw new SyntaxException($"P3 test is a if-else branch, require 'N' or 'Y' rather {branch}. In {code}");
                }

                int offset = branch is "N" ? 0 : 0b10000;
                CodeNode branchNode = new()
                {
                    Opcode = CalcCertainOpcode(offset, 3),
                };
                codeNode.NextNodes.Add(branchNode);
                codeNode.HasTest = 3;
                CompileLabel(label, basePlace + offset, branchNode);
            }
        }

        private void CompileTestP2(int idx, int basePlace, CodeNode codeNode)
        {
            basePlace &= 0b111100;
            for (; idx < codes.Length; idx++)
            {
                string code = codes[idx];
                string[] tokens = code.Trim(':').Split(':');
                if (tokens.Length != 2)
                {
                    return;
                }

                string branch = tokens[0].Trim();
                string label = tokens[1].Trim();
                int offset = Convert.ToInt32(branch, 16);

                if (offset != (offset & 0b11))
                {
                    throw new SyntaxException($"{branch} out of P2 branch range, which is 0 to 3. In {code}");
                }

                CodeNode branchNode = new()
                {
                    Opcode = CalcCertainOpcode(offset, 2),
                };
                codeNode.NextNodes.Add(branchNode);
                codeNode.HasTest = 2;
                CompileLabel(label, basePlace + offset, branchNode);
            }
        }

        private void CompileTestP1(int idx, int basePlace, CodeNode codeNode)
        {
            basePlace &= 0b110000;
            for (; idx < codes.Length; idx++)
            {
                string code = codes[idx];
                string[] tokens = code.Trim(':').Split(':');
                if (tokens.Length != 2)
                {
                    return;
                }

                string branch = tokens[0].Trim();
                string label = tokens[1].Trim();
                int offset = Convert.ToInt32(branch, 16);

                if (offset != (offset & 0b1111))
                {
                    throw new SyntaxException($"{branch} out of P1 branch range, which is 0 to F. In {code}");
                }

                CodeNode branchNode = new()
                {
                    Opcode = CalcCertainOpcode(offset, 1),
                };
                codeNode.NextNodes.Add(branchNode);
                codeNode.HasTest = 1;
                CompileLabel(label, basePlace + offset, branchNode);
            }
        }

        private void CompileLine(int lineIdx, int placeHint, int nextHint, CodeNode codeNode, string test = null)
        {
            string code = codes[lineIdx];
            codeNode.Code = code;
            codeNode.PlaceInReal = placeHint;

            if (!code.Contains('='))
            {
                asmCodes[placeHint] = TransNotAssign(code, placeHint, nextHint, test);
            }
            else
            {
                string[] tokens = code.Split('=');
                string l = tokens[0].Trim();
                string r = tokens[1].Trim();

                asmCodes[placeHint] = TransAssign(placeHint, l, r, nextHint, test);
            }
        }

        private int CompileLine(int lineIdx, int placeHint, CodeNode codeNode)
        {
            asmCodes[placeHint] = "Placeholder";
            int next = GetNextAvailableIndex();

            CompileLine(lineIdx, placeHint, next, codeNode);

            return next;
        }

        private void BuildLabelTable()
        {
            for (int i = 0; i < codes.Length; i++)
            {
                string code = codes[i];

                if (code.StartsWith('.'))
                {
                    if (codes[i + 1].StartsWith('.'))
                    {
                        throw new SyntaxException($"Empty label. In {code}");
                    }

                    code = code.Trim('.', ':');
                    string label = code.Trim('#', '!');

                    if (code.EndsWith('!'))
                    {
                        if (nextInstructionLabel is not null)
                        {
                            throw new SyntaxException($"More than one next instruction label. In {label}");
                        }
                        nextInstructionLabel = label;
                    }

                    labelTable[label] = i + 1;
                }
            }

            if (nextInstructionLabel is null)
            {
                throw new SyntaxException("Must have a next instruction label.");
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
                    int basePlace = Convert.ToInt32(tests[1], 16);
                    if (tests[0] == "P1")
                    {
                        basePlace &= 0b110000;
                        for (int o = 0; o < 0b1111; o++)
                        {
                            asmCodes[basePlace + o] = "Reserve";
                        }
                    }
                    else if (tests[0] == "P2")
                    {
                        basePlace &= 0b111100;
                        asmCodes[basePlace + 0] = "Reserve";
                        asmCodes[basePlace + 1] = "Reserve";
                        asmCodes[basePlace + 2] = "Reserve";
                        asmCodes[basePlace + 3] = "Reserve";

                    }
                    else if (tests[0] == "P3")
                    {
                        basePlace &= 0b101111;
                        asmCodes[basePlace + 0] = "Reserve";
                        asmCodes[basePlace + 0b10000] = "Reserve";
                    }
                    else if (tests[0] == "P4")
                    {
                        basePlace &= 0b011111;
                        asmCodes[basePlace + 0] = "Reserve";
                        asmCodes[basePlace + 0b100000] = "Reserve";
                    }
                }
            }
        }
    }
}
