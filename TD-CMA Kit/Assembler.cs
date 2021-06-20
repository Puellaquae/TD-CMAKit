using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using static TD_CMAKit.MicrocodeCompiler;

namespace TD_CMAKit
{
    public class Assembler
    {
        private readonly Dictionary<string, List<(string opcode, int bitLen)>> instructionInfTable;

        public Assembler(Dictionary<string, List<(string opcode, int bitLen)>> instructionInfTable)
        {
            this.instructionInfTable = instructionInfTable;
        }

        private void AssembleInstruct(string code, List<string> hex, Dictionary<string, int> labelRealIndexTable, List<(int, string)> labelSlots)
        {
            if (code.StartsWith('.'))
            {
                code = code.Trim('.', ':');
                labelRealIndexTable[code] = hex.Count;
            }
            else
            {
                string[] tokens = code.Split(' ', ',');
                string ist = tokens[0];
                if (!instructionInfTable.ContainsKey(ist))
                {
                    throw new SyntaxException($"{ist} is not a instruction in {code}");
                }
                var inf = instructionInfTable[ist];
                string currentOpcode;
                int currentBitLen;
                int startTokenIdx = 1;
                if (inf.Count == 1)
                {
                    (currentOpcode, currentBitLen) = inf[0];
                }
                else
                {
                    string mode = tokens[startTokenIdx];
                    if (!mode.StartsWith("M"))
                    {
                        throw new SyntaxException($"{ist} need mode select. In {code}");
                    }

                    int modei = Convert.ToInt32(mode.Trim('M'));
                    (currentOpcode, currentBitLen) = inf[modei];
                }

                bool hasRS = currentOpcode.Contains("RS");
                bool hasRD = currentOpcode.Contains("RD");
                string op = currentOpcode;
                int appdenBitLen = currentBitLen - 1;

                List<string> appdenBit = new();
                for (int i = startTokenIdx; i < tokens.Length; i++)
                {
                    string token = tokens[i].Trim();
                    if (token == "")
                    {
                        continue;
                    }

                    if (hasRD)
                    {
                        string r = token switch
                        {
                            "R1" => "00",
                            "R2" => "01",
                            "R3" => "10",
                            "R4" => "11",
                            _ => throw new SyntaxException($"{token} is not a register in {code}")
                        };

                        op = op[0..6] + r;
                        hasRD = false;
                    }
                    else if (hasRS)
                    {
                        string r = token switch
                        {
                            "R1" => "00",
                            "R2" => "01",
                            "R3" => "10",
                            "R4" => "11",
                            _ => throw new SyntaxException($"{token} is not a register in {code}")
                        };

                        op = op[0..4] + r + op[6..8];
                        hasRS = false;
                    }

                    else if (appdenBitLen > 0)
                    {
                        appdenBitLen--;
                        if (int.TryParse(token, out int num))
                        {
                            appdenBit.Add($"{num:X2}");
                        }
                        else
                        {
                            appdenBit.Add($"${{{token}}}");
                            labelSlots.Add((hex.Count + appdenBit.Count, token));
                        }
                    }
                    else
                    {
                        throw new SyntaxException($"{token} is not acceptable for {ist} in {code}");
                    }
                }
                hex.Add(Convert.ToInt32(op.Replace('X', '0'), 2).ToString("X2"));
                hex.AddRange(appdenBit);
            }
        }

        private void LinkLabelSlot(List<string> hex, Dictionary<string, int> labelRealIndexTable, List<(int, string)> labelSlots)
        {
            foreach ((int idx, string label) in labelSlots)
            {
                if (!labelRealIndexTable.ContainsKey(label))
                {
                    throw new SyntaxException($"{label} is not a label or immediate number");
                }
                hex[idx] = $"{labelRealIndexTable[label]:X2}";
            }
        }

        /// <summary>
        /// 将汇编转换为二进制指令
        /// </summary>
        /// <param name="codes"></param>
        /// <exception cref="SyntaxException"></exception>
        /// <returns></returns>
        public string[] Assemble(string[] codes)
        {
            List<string> hex = new();
            Dictionary<string, int> labelRealIndexTable = new();
            List<(int, string)> labelSlots = new();

            foreach (string code in codes)
            {
                AssembleInstruct(code, hex, labelRealIndexTable, labelSlots);
            }

            LinkLabelSlot(hex, labelRealIndexTable, labelSlots);

            for (int i = 0; i < hex.Count; i++)
            {
                hex[i] = $"$P {i:X2} " + hex[i];
            }

            return hex.ToArray();
        }
    }
}
