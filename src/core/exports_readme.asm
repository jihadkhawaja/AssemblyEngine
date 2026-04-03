; ============================================================================
; AssemblyEngine - DLL Exports Module
; Defines all exported symbols for the assembled DLL
; Target: x86-64 Windows (NASM)
; ============================================================================

; This file is linked into assemblycore.dll and re-exports all public symbols.
; Each module declares its own globals; this .def file lists them for the linker.
;
; Build command:
;   nasm -f win64 platform_win64.asm -o platform_win64.obj
;   nasm -f win64 renderer.asm -o renderer.obj
;   nasm -f win64 sprite.asm -o sprite.obj
;   nasm -f win64 input.asm -o input.obj
;   nasm -f win64 timer.asm -o timer.obj
;   nasm -f win64 memory.asm -o memory.obj
;   nasm -f win64 audio.asm -o audio.obj
;   nasm -f win64 math.asm -o math.obj
;
;   link /DLL /OUT:assemblycore.dll /DEF:exports.def \
;        platform_win64.obj renderer.obj sprite.obj input.obj \
;        timer.obj memory.obj audio.obj math.obj \
;        kernel32.lib user32.lib gdi32.lib winmm.lib
