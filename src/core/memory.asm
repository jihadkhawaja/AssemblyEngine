; ============================================================================
; AssemblyEngine - Memory System
; Arena allocator using VirtualAlloc
; Target: x86-64 Windows (NASM)
; ============================================================================

%include "include/engine_state.inc"

extern g_engine
extern VirtualAlloc
extern VirtualFree

global ae_memory_init
global ae_memory_shutdown
global ae_alloc
global ae_free

; Default arena: 64 MB
%define ARENA_SIZE (64 * 1024 * 1024)

section .text

; ============================================================================
; ae_memory_init() - Allocate the main memory arena
; ============================================================================
ae_memory_init:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 40

    lea rdi, [rel g_engine]

    ; VirtualAlloc(NULL, size, MEM_COMMIT|MEM_RESERVE, PAGE_READWRITE)
    xor ecx, ecx                        ; lpAddress = NULL
    mov rdx, ARENA_SIZE                 ; dwSize
    mov r8d, MEM_COMMIT | MEM_RESERVE   ; flAllocationType
    mov r9d, PAGE_READWRITE             ; flProtect
    call VirtualAlloc

    mov [rdi + EngineState.arena_base], rax
    mov qword [rdi + EngineState.arena_offset], 0
    mov qword [rdi + EngineState.arena_size], ARENA_SIZE

    ; Also allocate sprite descriptor array
    xor ecx, ecx
    mov edx, AE_MAX_SPRITES * SpriteDesc_size
    mov r8d, MEM_COMMIT | MEM_RESERVE
    mov r9d, PAGE_READWRITE
    call VirtualAlloc
    lea rdi, [rel g_engine]
    mov [rdi + EngineState.sprites], rax
    mov dword [rdi + EngineState.sprite_count], 0

    add rsp, 40
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_memory_shutdown() - Free the memory arena
; ============================================================================
ae_memory_shutdown:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 40

    lea rdi, [rel g_engine]

    ; Free sprite array
    mov rcx, [rdi + EngineState.sprites]
    test rcx, rcx
    jz .no_sprites
    xor edx, edx
    mov r8d, MEM_RELEASE
    call VirtualFree
.no_sprites:

    ; Free arena
    lea rdi, [rel g_engine]
    mov rcx, [rdi + EngineState.arena_base]
    test rcx, rcx
    jz .no_arena
    xor edx, edx
    mov r8d, MEM_RELEASE
    call VirtualFree
.no_arena:

    lea rdi, [rel g_engine]
    mov qword [rdi + EngineState.arena_base], 0
    mov qword [rdi + EngineState.arena_offset], 0

    add rsp, 40
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_alloc(size: ecx) -> rax (pointer, NULL on failure)
; Bump allocator from the arena (16-byte aligned)
; ============================================================================
ae_alloc:
    lea rax, [rel g_engine]

    ; Align size to 16 bytes
    add ecx, 15
    and ecx, ~15
    movsxd rcx, ecx

    ; Check if enough space
    mov rdx, [rax + EngineState.arena_offset]
    add rdx, rcx
    cmp rdx, [rax + EngineState.arena_size]
    ja .oom

    ; Return current position, advance offset
    mov r8, [rax + EngineState.arena_base]
    add r8, [rax + EngineState.arena_offset]
    mov [rax + EngineState.arena_offset], rdx
    mov rax, r8
    ret

.oom:
    xor eax, eax
    ret

; ============================================================================
; ae_free() - Resets arena (frees all allocations at once)
; For per-frame temporary allocations
; ============================================================================
ae_free:
    lea rax, [rel g_engine]
    mov qword [rax + EngineState.arena_offset], 0
    ret
