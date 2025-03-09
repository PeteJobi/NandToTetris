// This file is part of www.nand2tetris.org
// and the book "The Elements of Computing Systems"
// by Nisan and Schocken, MIT Press.
// File name: projects/4/Fill.asm

// Runs an infinite loop that listens to the keyboard input. 
// When a key is pressed (any key), the program blackens the screen,
// i.e. writes "black" in every pixel. When no key is pressed, 
// the screen should be cleared.

@SCREEN
D=A
@scr
M=D

(LOOP)
@KBD
D=M
@BLACKEN
D;JNE
@scr
A=M
M=0
@END
0;JMP
(BLACKEN)
@scr
A=M
M=-1

(END)
@scr
M=M+1
@KBD
D=A
@scr
D=M-D
@LOOP
D;JLT
@SCREEN
D=A
@scr
M=D
@LOOP
0;JMP