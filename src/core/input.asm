; ============================================================================
; AssemblyEngine - Input System
; Keyboard and mouse state queries
; Target: x86-64 Windows (NASM)
; ============================================================================

%include "include/engine_state.inc"

extern g_engine

global ae_is_key_down
global ae_is_key_pressed
global ae_get_mouse_x
global ae_get_mouse_y
global ae_is_mouse_down

section .text

; ============================================================================
; ae_is_key_down(keycode: ecx) -> eax (1=down, 0=up)
; Returns whether a key is currently held down
; ============================================================================
ae_is_key_down:
    lea rax, [rel g_engine]
    and ecx, 0xFF
    movzx eax, byte [rax + EngineState.keys + rcx]
    ret

; ============================================================================
; ae_is_key_pressed(keycode: ecx) -> eax (1=just pressed, 0=not)
; Returns true only on the frame the key was first pressed
; ============================================================================
ae_is_key_pressed:
    lea rax, [rel g_engine]
    and ecx, 0xFF
    movzx edx, byte [rax + EngineState.keys + rcx]
    movzx r8d, byte [rax + EngineState.prev_keys + rcx]
    ; pressed = current && !previous
    test edx, edx
    jz .not_pressed
    test r8d, r8d
    jnz .not_pressed
    mov eax, 1
    ret
.not_pressed:
    xor eax, eax
    ret

; ============================================================================
; ae_get_mouse_x() -> eax
; ============================================================================
ae_get_mouse_x:
    lea rax, [rel g_engine]
    mov eax, [rax + EngineState.mouse_x]
    ret

; ============================================================================
; ae_get_mouse_y() -> eax
; ============================================================================
ae_get_mouse_y:
    lea rax, [rel g_engine]
    mov eax, [rax + EngineState.mouse_y]
    ret

; ============================================================================
; ae_is_mouse_down(button: ecx) -> eax (1=down, 0=up)
; button: 0=left, 1=right, 2=middle
; ============================================================================
ae_is_mouse_down:
    lea rax, [rel g_engine]
    and ecx, 3
    mov edx, [rax + EngineState.mouse_buttons]
    bt edx, ecx
    setc al
    movzx eax, al
    ret
