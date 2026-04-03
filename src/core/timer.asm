; ============================================================================
; AssemblyEngine - Timer System
; High-precision timing using QueryPerformanceCounter
; Target: x86-64 Windows (NASM)
; ============================================================================

%include "include/engine_state.inc"

extern g_engine
extern QueryPerformanceFrequency
extern QueryPerformanceCounter

global ae_timer_init
global ae_timer_update
global ae_get_delta_time
global ae_get_fps
global ae_get_ticks

section .data
    one_second  dd 1.0

section .text

; ============================================================================
; ae_timer_init() - Initialize performance counter frequency
; ============================================================================
ae_timer_init:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 40

    lea rdi, [rel g_engine]

    ; Get performance frequency
    lea rcx, [rdi + EngineState.perf_freq]
    call QueryPerformanceFrequency

    ; Get initial tick
    lea rcx, [rdi + EngineState.last_tick]
    call QueryPerformanceCounter

    ; Initialize FPS tracking
    mov dword [rdi + EngineState.frame_count], 0
    xor eax, eax
    mov [rdi + EngineState.fps_accum], eax
    mov dword [rdi + EngineState.fps], 0

    add rsp, 40
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_timer_update() - Calculate delta time for this frame
; Called once per frame from ae_poll_events
; ============================================================================
ae_timer_update:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 56

    lea rdi, [rel g_engine]

    ; Save previous tick
    mov rax, [rdi + EngineState.last_tick]
    mov [rbp-8], rax

    ; Get current tick
    lea rcx, [rdi + EngineState.last_tick]
    call QueryPerformanceCounter

    ; delta_ticks = current - previous
    lea rdi, [rel g_engine]
    mov rax, [rdi + EngineState.last_tick]
    sub rax, [rbp-8]
    mov [rdi + EngineState.current_tick], rax

    ; delta_time = delta_ticks / frequency (as float)
    cvtsi2ss xmm0, rax
    cvtsi2ss xmm1, qword [rdi + EngineState.perf_freq]
    divss xmm0, xmm1
    movss [rdi + EngineState.delta_time], xmm0

    ; Clamp delta_time to 0.1s max (prevent spiral of death)
    mov eax, 0x3DCCCCCD                 ; 0.1f
    movd xmm1, eax
    comiss xmm0, xmm1
    jbe .dt_ok
    movss [rdi + EngineState.delta_time], xmm1
.dt_ok:

    ; FPS calculation
    movss xmm0, [rdi + EngineState.fps_accum]
    addss xmm0, [rdi + EngineState.delta_time]
    movss [rdi + EngineState.fps_accum], xmm0

    inc dword [rdi + EngineState.frame_count]

    ; Check if 1 second elapsed
    movss xmm1, [rel one_second]
    comiss xmm0, xmm1
    jb .no_fps_update

    ; FPS = frame_count / accumulated_time
    cvtsi2ss xmm1, dword [rdi + EngineState.frame_count]
    divss xmm1, xmm0
    cvtss2si eax, xmm1
    mov [rdi + EngineState.fps], eax
    mov dword [rdi + EngineState.frame_count], 0
    xorps xmm0, xmm0
    movss [rdi + EngineState.fps_accum], xmm0

.no_fps_update:
    add rsp, 56
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_get_delta_time() -> xmm0 (float, also eax as bits)
; Returns delta time in seconds as a float
; ============================================================================
ae_get_delta_time:
    lea rax, [rel g_engine]
    movss xmm0, [rax + EngineState.delta_time]
    movd eax, xmm0                      ; also return as int bits for interop
    ret

; ============================================================================
; ae_get_fps() -> eax
; ============================================================================
ae_get_fps:
    lea rax, [rel g_engine]
    mov eax, [rax + EngineState.fps]
    ret

; ============================================================================
; ae_get_ticks() -> rax (raw QPC ticks since last frame)
; ============================================================================
ae_get_ticks:
    lea rax, [rel g_engine]
    mov rax, [rax + EngineState.current_tick]
    ret
