using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TD_CMAKit.MicrocodeCompiler;

namespace TD_CMAKit
{
    public class Assembler
    {
        private Dictionary<string, InstructionInf> instructionInfTable;
        private Dictionary<string, int> labelRealIndexTable;
        private List<(int, string)> labelSlots;

        public Assembler(Dictionary<string, InstructionInf> instructionInfTable)
        {
            this.instructionInfTable = instructionInfTable;
        }

        public string[] assemble(string[] codes)
        {
            throw new NotImplementedException();
        }
    }
}
