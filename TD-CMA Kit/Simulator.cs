using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TD_CMAKit.MicrocodeAssembler;

namespace TD_CMAKit
{
    public class Simulator
    {
        private List<uint> Microcode { get; set; } = new(new uint[256]);
        public List<byte> Memory { get; set; } = new(new byte[256]);

        public struct CPUState
        {
            public byte R0, R1, R2, R3, A, B;
            public byte PC, IR, AR;
            public bool FC, FZ;
            public int MicroCodeIR;
        }

        public CPUState State { get; private set; }

        public Simulator(string[] microcode, string[] memory)
        {
            
            foreach (string s in microcode)
            {
                string[] ss = s.Split(' ');
                int p = Convert.ToByte(ss[1], 16);
                uint c = Convert.ToUInt32(ss[2], 16);
                Microcode[p] = c;
            }

            foreach (string s in memory)
            {
                string[] ss = s.Split(' ');
                int p = Convert.ToByte(ss[1], 16);
                byte c = Convert.ToByte(ss[2], 16);
                Memory[p] = c;
            }
            
            throw new NotImplementedException("Simulator is developing");
        }

        private byte CycleShiftRight(byte val, byte iShiftBit)
        {
            byte temp = 0;
            temp |= val;
            val >>= iShiftBit;
            temp <<= 8 - iShiftBit;
            byte result = (byte)(val | temp);
            return result;
        }
        private byte CycleShiftLeft(byte val, byte iShiftBit)
        {
            byte temp = 0;
            temp |= val;
            val <<= iShiftBit;
            temp >>= 8 - iShiftBit;
            byte result = (byte)(val | temp);
            return result;
        }

        public void Next()
        {
            CPUState newState = State;
            uint mc = Microcode[State.MicroCodeIR];

            bool CN = (mc & 0x400000) != 0;
            bool WR = (mc & 0x200000) != 0;
            bool RD = (mc & 0x100000) != 0;
            bool IOM = (mc & 0x080000) != 0;

            uint UA = mc & 0b11111;
            uint FleidC = (mc >> 5) & 0b111;
            uint FleidB = (mc >> 8) & 0b111;
            uint FleidA = (mc >> 12) & 0b111;
            uint S = (mc >> 15) & 0b1111;

            var rs = (State.IR >> 2) & 0b11;
            var rd = State.IR & 0b11;

            A a = (A)FleidA;
            B b = (B)FleidB;
            C c = (C)FleidC;
            ALU alu = (ALU)S;

            byte bus;

            Func<byte, byte, byte> calu = (byte a, byte b) =>
            {
                byte ans = 0;
                switch (alu)
                {
                    case ALU.A:
                        return a;
                    case ALU.B:
                        return b;
                    case ALU.AND:
                        newState.FZ = (a & b) == 0;
                        return (byte)(a & b);
                    case ALU.OR:
                        newState.FZ = (a | b) == 0;
                        return (byte)(a | b);
                    case ALU.NOT:
                        newState.FZ = (~a) == 0;
                        return (byte)~a;
                    case ALU.ROR:
                        ans = CycleShiftRight(a, (byte)(b & 0b111));
                        newState.FZ = ans == 0;
                        return ans;
                    case ALU.SR:
                        ans = State.FC ? (byte)(a >> 1) : CycleShiftRight(a, 1);
                        newState.FZ = ans == 0;
                        newState.FC = (ans & 0x80) != 0;
                        return ans;
                    case ALU.SL:
                        ans = State.FC ? (byte)(a << 1) : CycleShiftLeft(a, 1);
                        newState.FZ = ans == 0;
                        newState.FC = (ans & 0x80) != 0;
                        return ans;
                    case ALU.CN:
                        newState.FC = CN;
                        return 0;
                    case ALU.ADD:
                        newState.FZ = (a + b) == 0;
                        newState.FC = ((a & 0x80) & (b & 0x80)) != 0;
                        return (byte)(a + b);
                    case ALU.ADC:
                        newState.FZ = (a + b + (State.FC ? 1 : 0)) == 0;
                        newState.FC = (State.FC ? ((a & 0x80) | (b & 0x80)) : ((a & 0x80) & (b & 0x80))) != 0;
                        return (byte)(a + b + (State.FC ? 1 : 0));
                    case ALU.SUB:
                        newState.FZ = (a - b) == 0;
                        newState.FC = a < b;
                        return (byte)(a - b);
                    case ALU.DEC:
                        newState.FZ = (a - 1) == 0;
                        newState.FC = a < 1;
                        return (byte)(a - 1);
                    case ALU.INC:
                        newState.FZ = (a + 1) == 0;
                        newState.FC = a < 0xff;
                        return (byte)(a + 1);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            };

            if (RD && IOM)
            {
                bus = (byte)IO;
            }
            else if (RD && !IOM)
            {
                bus = Memory[State.AR];
            }


            bus = b switch
            {
                B.NOP => 0,
                B.ALU_B => calu(State.A, State.B),
                B.RS_B => (rs) switch
                {
                    0 => State.R0,
                    1 => State.R1,
                    2 => State.R2,
                    3 => State.R3,
                },
                B.RD_B => (rd) switch
                {
                    0 => State.R0,
                    1 => State.R1,
                    2 => State.R2,
                    3 => State.R3,
                },
                B.RI_B => State.R2,
                B.SP_B => State.R3,
                B.PC_B => State.PC,
                _ => throw new ArgumentOutOfRangeException()
            };

            switch (a)
            {
                case A.NOP:
                    break;
                case A.LDA:
                    newState.A = bus;
                    break;
                case A.LDB:
                    newState.B = bus;
                    break;
                case A.LDRi:
                    switch (rd)
                    {
                        case 0:
                            newState.R0 = bus;
                            break;
                        case 1:
                            newState.R1 = bus;
                            break;
                        case 2:
                            newState.R2 = bus;
                            break;
                        case 3:
                            newState.R3 = bus;
                            break;
                    }
                    break;
                case A.LDSP:
                    newState.R3 = bus;
                    break;
                case A.LOAD:
                    newState.PC = bus;
                    break;
                case A.LDAR:
                    newState.AR = bus;
                    break;
                case A.LDIR:
                    newState.IR = bus;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (WR & IOM)
            {
                IO = bus;
            }
            else if (WR)
            {
                Memory[newState.AR] = bus;
            }

            newState.MicroCodeIR = (int)UA;
            switch (c)
            {
                case C.NOP:

                    break;
                case C.P1:
                    if ((newState.IR >> 6) != 3)
                    {
                        newState.MicroCodeIR = (byte)((UA & 0b11110000) | (uint)((newState.IR >> 4) & 0b1111));
                    }
                    else
                    {
                        newState.MicroCodeIR = (byte)((UA & 0b11111100) | (uint)((newState.IR >> 2) & 0b11));
                    }
                    break;
                case C.P2:
                    newState.MicroCodeIR = (byte)((UA & 0b11111100) | (uint)((newState.IR >> 4) & 0b11));
                    break;
                case C.P3:
                    newState.MicroCodeIR = (byte)((UA & 0b10111111) | (uint)(newState.FC || newState.FZ ? 0b10000 : 0));
                    break;
                case C.P4:
                    throw new NotImplementedException();
                case C.LDPC:
                    if (a != A.LOAD)
                    {
                        newState.PC++;
                    }

                    break;
                case C.STI:
                    throw new NotImplementedException();
                case C.CLI:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }

            State = newState;
        }

        private int IN, OUT;

        public int IO
        {
            get => OUT;
            set => IN = value;
        }
    }
}
