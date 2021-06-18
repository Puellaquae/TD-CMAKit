using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit.Sdk;
using TD_CMAKit;
using System.Collections.Generic;

namespace UnitTest
{
    [TestClass]
    public class AssemblerTest
    {
        [TestMethod]
        public void NOP()
        {
            Assert.AreEqual("$M 00 000001", MicrocodeAssembler.Translate("00 01").ToString());
            Assert.AreEqual("$M 3E 00001D", MicrocodeAssembler.Translate("3E 1D").ToString());
            Assert.AreEqual("$M 12 00003F", MicrocodeAssembler.Translate("12 3F").ToString());
        }

        [TestMethod]
        public void Asm2Hex()
        {
            Assert.AreEqual("$M 01 006D43", MicrocodeAssembler.Translate("01 LDAR PC_B LDPC 03").ToString());
            Assert.AreEqual("$M 03 107070", MicrocodeAssembler.Translate("03 RD LDIR P1 30").ToString());
            Assert.AreEqual("$M 04 002405", MicrocodeAssembler.Translate("04 LDB RS_B 05").ToString());
            Assert.AreEqual("$M 05 04B201", MicrocodeAssembler.Translate("05 ADD LDRi ALU_B 01").ToString());
            Assert.AreEqual("$M 06 002407", MicrocodeAssembler.Translate("06 LDB RS_B 07").ToString());
            Assert.AreEqual("$M 07 013201", MicrocodeAssembler.Translate("07 AND LDRi ALU_B 01").ToString());
            Assert.AreEqual("$M 08 106009", MicrocodeAssembler.Translate("08 RD LDAR 09").ToString());
            Assert.AreEqual("$M 09 183001", MicrocodeAssembler.Translate("09 RD IOM LDRi 01").ToString());
            Assert.AreEqual("$M 0A 106010", MicrocodeAssembler.Translate("0A RD LDAR 10").ToString());
            Assert.AreEqual("$M 0E 005341", MicrocodeAssembler.Translate("0E LOAD ALU_B LDPC 01").ToString());
        }
    }

    [TestClass]
    public class CompilerTest
    {
        string AssignHelper(string code)
        {
            string[] tokens = code.Split(' ');
            string[] assign = tokens[1].Split('=');
            MicrocodeCompiler compiler = new MicrocodeCompiler(new string[1]);
            int bitlen = 0;
            return MicrocodeAssembler.Translate(compiler.TransAssign(
                int.Parse(tokens[0], NumberStyles.HexNumber), 
                assign[0],
                assign[1],
                int.Parse(tokens[2], NumberStyles.HexNumber),
                ref bitlen)).ToString();
        }
        [TestMethod]
        public void Assign()
        {
            Assert.AreEqual("$M 04 002405", AssignHelper("04 B=RS 05"));
            Assert.AreEqual("$M 05 04B201", AssignHelper("05 RD=A+B 01"));
            Assert.AreEqual("$M 08 106009", AssignHelper("08 AR=MEM 09"));
            Assert.AreEqual("$M 01 006D43", AssignHelper("01 AR=PC 03"));
            Assert.AreEqual("$M 16 01B201", AssignHelper("16 RD=A|B 01"));
            Assert.AreEqual("$M 14 05B201", AssignHelper("14 RD=A-B 01"));
            Assert.AreEqual("$M 0C 103001", AssignHelper("0C RD=MEM 01"));
            Assert.AreEqual("$M 0D 200601", AssignHelper("0D MEM=RD 01"));
            Assert.AreEqual("$M 32 006D48", AssignHelper("32 AR=PC 08"));
        }
    }
}
