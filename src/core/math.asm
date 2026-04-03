; ============================================================================
; AssemblyEngine - Math Utilities
; Fixed-point and integer math for 2D operations
; Target: x86-64 Windows (NASM)
; ============================================================================

%include "include/engine_state.inc"

global ae_math_clamp
global ae_math_lerp
global ae_math_min
global ae_math_max
global ae_math_abs
global ae_math_sqrt_int
global ae_math_dist_sq

section .data
    align 16
    float_half  dd 0.5

section .text

; ============================================================================
; ae_math_clamp(value: ecx, min: edx, max: r8d) -> eax
; ============================================================================
ae_math_clamp:
    mov eax, ecx
    cmp eax, edx
    cmovl eax, edx
    cmp eax, r8d
    cmovg eax, r8d
    ret

; ============================================================================
; ae_math_lerp(a: xmm0, b: xmm1, t: xmm2) -> xmm0
; Returns a + (b - a) * t
; ============================================================================
ae_math_lerp:
    subss xmm1, xmm0                   ; b - a
    mulss xmm1, xmm2                   ; (b - a) * t
    addss xmm0, xmm1                   ; a + (b - a) * t
    ret

; ============================================================================
; ae_math_min(a: ecx, b: edx) -> eax
; ============================================================================
ae_math_min:
    cmp ecx, edx
    mov eax, ecx
    cmovg eax, edx
    ret

; ============================================================================
; ae_math_max(a: ecx, b: edx) -> eax
; ============================================================================
ae_math_max:
    cmp ecx, edx
    mov eax, ecx
    cmovl eax, edx
    ret

; ============================================================================
; ae_math_abs(value: ecx) -> eax
; ============================================================================
ae_math_abs:
    mov eax, ecx
    cdq
    xor eax, edx
    sub eax, edx
    ret

; ============================================================================
; ae_math_sqrt_int(value: ecx) -> eax
; Integer square root via Newton's method
; ============================================================================
ae_math_sqrt_int:
    test ecx, ecx
    jz .zero
    js .zero

    cvtsi2ss xmm0, ecx
    sqrtss xmm0, xmm0
    cvtss2si eax, xmm0
    ret

.zero:
    xor eax, eax
    ret

; ============================================================================
; ae_math_dist_sq(x1: ecx, y1: edx, x2: r8d, y2: r9d) -> eax
; Returns squared distance between two points (avoids sqrt)
; ============================================================================
ae_math_dist_sq:
    sub r8d, ecx                        ; dx = x2 - x1
    sub r9d, edx                        ; dy = y2 - y1
    imul r8d, r8d                        ; dx^2
    imul r9d, r9d                        ; dy^2
    lea eax, [r8d + r9d]               ; dx^2 + dy^2
    ret
