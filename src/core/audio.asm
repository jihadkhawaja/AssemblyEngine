; ============================================================================
; AssemblyEngine - Audio System
; Simple WAV playback via PlaySound and waveOut
; Target: x86-64 Windows (NASM)
; ============================================================================

%include "include/engine_state.inc"

extern g_engine
extern ae_alloc
extern CreateFileA
extern ReadFile
extern CloseHandle
extern GetFileSize
extern PlaySoundA

global ae_load_sound
global ae_play_sound
global ae_stop_sound

%define GENERIC_READ        0x80000000
%define OPEN_EXISTING       3
%define FILE_ATTRIBUTE_NORMAL 0x80
%define INVALID_HANDLE_VALUE -1

section .text

; ============================================================================
; ae_load_sound(path: rcx) -> eax (sound_id, -1 on error)
; Loads a WAV file into memory for later playback
; ============================================================================
ae_load_sound:
    SAVE_NONVOL
    push rbp
    mov rbp, rsp
    sub rsp, 96

    mov [rbp-8], rcx                    ; path

    lea rdi, [rel g_engine]
    mov eax, [rdi + EngineState.sound_count]
    cmp eax, AE_MAX_SOUNDS
    jge .error
    mov [rbp-12], eax                   ; slot index

    ; Open file
    mov rcx, [rbp-8]
    mov edx, GENERIC_READ
    xor r8d, r8d
    xor r9d, r9d
    sub rsp, 32
    push 0
    push FILE_ATTRIBUTE_NORMAL
    push OPEN_EXISTING
    call CreateFileA
    add rsp, 56
    cmp rax, INVALID_HANDLE_VALUE
    je .error
    mov [rbp-24], rax                   ; handle

    ; Get size
    mov rcx, rax
    xor edx, edx
    call GetFileSize
    mov [rbp-28], eax

    ; Allocate buffer
    mov ecx, eax
    call ae_alloc
    test rax, rax
    jz .close_err
    mov [rbp-40], rax                   ; buffer

    ; Read
    mov rcx, [rbp-24]
    mov rdx, rax
    mov r8d, [rbp-28]
    lea r9, [rbp-48]
    push 0
    sub rsp, 32
    call ReadFile
    add rsp, 40

    ; Close
    mov rcx, [rbp-24]
    call CloseHandle

    ; Store sound descriptor
    lea rdi, [rel g_engine]
    mov rax, [rdi + EngineState.sounds]
    test rax, rax
    jz .alloc_sounds

.store:
    mov ecx, [rbp-12]
    imul ecx, SoundDesc_size
    movsxd rcx, ecx
    add rax, rcx

    mov rcx, [rbp-40]
    mov [rax + SoundDesc.data], rcx
    mov ecx, [rbp-28]
    mov [rax + SoundDesc.size], ecx
    mov dword [rax + SoundDesc.flags], SND_MEMORY | SND_ASYNC | SND_NODEFAULT

    inc dword [rdi + EngineState.sound_count]
    mov eax, [rbp-12]
    jmp .done

.alloc_sounds:
    ; Lazy allocate sound array
    push rdi
    mov ecx, AE_MAX_SOUNDS * SoundDesc_size
    call ae_alloc
    pop rdi
    mov [rdi + EngineState.sounds], rax
    jmp .store

.close_err:
    mov rcx, [rbp-24]
    call CloseHandle

.error:
    mov eax, -1

.done:
    mov rsp, rbp
    pop rbp
    RESTORE_NONVOL
    ret

; ============================================================================
; ae_play_sound(id: ecx) -> eax (1=ok, 0=fail)
; ============================================================================
ae_play_sound:
    push rbp
    mov rbp, rsp
    sub rsp, 32

    lea rax, [rel g_engine]

    test ecx, ecx
    js .fail
    cmp ecx, [rax + EngineState.sound_count]
    jge .fail

    ; Get sound descriptor
    mov rdx, [rax + EngineState.sounds]
    imul ecx, SoundDesc_size
    movsxd rcx, ecx
    add rdx, rcx

    ; PlaySound(data, NULL, flags)
    mov rcx, [rdx + SoundDesc.data]
    xor edx, edx
    mov r8d, SND_MEMORY | SND_ASYNC | SND_NODEFAULT
    call PlaySoundA

    mov eax, 1
    jmp .done

.fail:
    xor eax, eax
.done:
    mov rsp, rbp
    pop rbp
    ret

; ============================================================================
; ae_stop_sound() - Stops all playing sounds
; ============================================================================
ae_stop_sound:
    push rbp
    mov rbp, rsp
    sub rsp, 32

    xor ecx, ecx                        ; NULL stops playback
    xor edx, edx
    xor r8d, r8d
    call PlaySoundA

    mov rsp, rbp
    pop rbp
    ret
