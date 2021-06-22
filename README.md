# TD-CMAKit

> 磨刀不误砍柴工

2020/2021(2) 计算机组成原理 TD-CMA 指令微指令相关工具

目前实现

- 微指令的汇编器，并推测指令格式信息
- 根据微指令生成流程图
- 根据推测信息进行汇编

## 例子

### 微指令汇编

```
.START!:
	AR=PC++
	IR=MEM
	<P1:30>
		0:ADD
		2:IN
		4:OUT
		5:HLT
		C:JMP

.ADD#:
	A=RS
	B=RD
	RD=A+B
	END

.IN#:
	AR=MEM
	RD=IN
	END

.OUT#:
	AR=MEM
	OUT=RS
	END

.HLT#:
	NOP
	END HLT

.JMP#:
	AR=PC++
	PC=MEM
	END
```

START 是程序处理的入口

标号后 ! 号表示此为读取下一指令的入口，END 默认会跳转至此

END 与 GOTO 区别在于绘制流程图时 END 会中止处理后继，请合理使用 GOTO 和 END 以避免绘制流图时出现环。

需要在标号后添加 # 指示程序这是一条指令

### 伪微指令

```
00 LDAR PC_B LDPC 01
01 LDIR RD P1 30
02 LDB RD_B 03
03 LDRi ADD ALU_B 00
04 LDRi RD IOM 00
05 WR IOM RS_B 00
06 LOAD LDPC RD 00
30 LDA RS_B 02
32 LDAR RD 04
34 LDAR RD 05
35 NOP 35
3C LDAR PC_B LDPC 06
```

### 二进制

```
$M 00 006D41
$M 01 107070
$M 02 002603
$M 03 04B200
$M 04 183000
$M 05 280400
$M 06 105140
$M 30 001402
$M 32 106004
$M 34 106005
$M 35 000035
$M 3C 006D46
```

### 根据微指令给出的指令提示
```
ADD: Mode 0 0000RSRD, 1 bits.
IN: Mode 0 0010XXRD, 1 bits.
OUT: Mode 0 0100RSXX, 1 bits.
HLT: Mode 0 0101XXXX, 1 bits.
JMP: Mode 0 11XX00XX, 2 bits.
```

### 微指令流程图

![流程图](Sample/Microcode/Simple.txt.png)

### 汇编

```
.START:
    IN R1
    OUT R1
    JMP START
```

程序根据从上到下的顺序处理代码

汇编参数顺序：先 RD，再 RS，最后依次的附加字节

### 二进制

```
$P 00 20
$P 01 40
$P 02 C0
$P 03 00
```