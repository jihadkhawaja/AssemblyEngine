; ============================================================================
; AssemblyEngine - Sprite System
; Loading and drawing sprites (BMP format)
; Target: x86-64 Windows (NASM)
; ============================================================================

%include "include/engine_state.inc"

extern g_engine
extern ae_alloc

global ae_load_sprite
global ae_draw_sprite

; --- Win32 file API ---
extern CreateFileA
extern ReadFile
extern CloseHandle
extern GetFileSize

; --- File access constants ---
%define GENERIC_READ        0x80000000
%define OPEN_EXISTING       3
%define FILE_ATTRIBUTE_NORMAL 0x80
%define INVALID_HANDLE_VALUE -1

section .text

; ============================================================================
; ae_load_sprite(path: rcx) -> eax (sprite_id, -1 on error)
; Loads a 32-bit BMP file into sprite slot
; ============================================================================
ae_load_sprite:
    SAVE_NONVOL
    push rbp
    mov rbp, rsp
    sub rsp, 160

    mov [rbp-8], rcx                    ; path

    lea rdi, [rel g_engine]
    mov eax, [rdi + EngineState.sprite_count]
    cmp eax, AE_MAX_SPRITES
    jge .error

    mov [rbp-12], eax                   ; slot index

    ; Open file
    mov rcx, [rbp-8]                    ; lpFileName
    mov edx, GENERIC_READ              ; dwDesiredAccess
    xor r8d, r8d                        ; dwShareMode
    xor r9d, r9d                        ; lpSecurityAttributes
    sub rsp, 32
    push 0                              ; hTemplateFile
    push FILE_ATTRIBUTE_NORMAL         ; dwFlagsAndAttributes
    push OPEN_EXISTING                 ; dwCreationDisposition
    call CreateFileA
    add rsp, 56
    cmp rax, INVALID_HANDLE_VALUE
    je .error
    mov [rbp-24], rax                   ; file handle

    ; Get file size
    mov rcx, rax
    xor edx, edx
    call GetFileSize
    mov [rbp-28], eax                   ; file size

    ; Allocate buffer for entire file
    mov ecx, eax
    call ae_alloc
    test rax, rax
    jz .close_error
    mov [rbp-40], rax                   ; file buffer

    ; Read entire file
    mov rcx, [rbp-24]                   ; hFile
    mov rdx, rax                        ; lpBuffer
    mov r8d, [rbp-28]                   ; nBytesToRead
    lea r9, [rbp-48]                    ; lpBytesRead
    push 0                              ; lpOverlapped
    sub rsp, 32
    call ReadFile
    add rsp, 40

    ; Close file
    mov rcx, [rbp-24]
    call CloseHandle

    ; Parse BMP header
    mov rsi, [rbp-40]                   ; file data

    ; Validate 'BM' signature
    cmp word [rsi], 0x4D42             ; 'BM'
    jne .error

    ; Get pixel data offset
    mov eax, [rsi+10]                   ; bfOffBits
    mov [rbp-52], eax

    ; Get image dimensions from DIB header
    mov eax, [rsi+18]                   ; biWidth
    mov [rbp-56], eax
    mov ecx, [rsi+22]                   ; biHeight (may be negative)
    mov [rbp-60], ecx

    ; Get bits per pixel
    movzx eax, word [rsi+28]            ; biBitCount
    cmp eax, 32
    jne .try_24bit
    jmp .parse_pixels

.try_24bit:
    cmp eax, 24
    jne .error

.parse_pixels:
    ; Calculate pixel data size
    mov eax, [rbp-56]                   ; width
    mov ecx, [rbp-60]                   ; height
    test ecx, ecx
    jns .height_pos
    neg ecx                              ; abs(height)
.height_pos:
    mov [rbp-64], ecx                    ; abs height

    ; Allocate sprite pixel buffer (width * height * 4)
    mov eax, [rbp-56]
    imul eax, ecx
    shl eax, 2                           ; * 4 bytes per pixel
    mov [rbp-68], eax                    ; pixel buffer size
    mov ecx, eax
    call ae_alloc
    test rax, rax
    jz .error
    mov [rbp-80], rax                    ; pixel buffer ptr

    ; Copy pixels (handle bottom-up BMPs)
    mov rsi, [rbp-40]                   ; file data
    mov eax, [rbp-52]
    add rsi, rax                        ; source = file + pixel offset

    mov rdi, [rbp-80]                   ; destination
    mov ecx, [rbp-56]                   ; width
    mov edx, [rbp-64]                   ; height
    imul ecx, edx                        ; total pixels

    ; Check if bottom-up (positive biHeight)
    cmp dword [rbp-60], 0
    jl .top_down_copy

    ; Bottom-up: need to flip rows
    mov eax, [rbp-56]
    shl eax, 2                           ; row bytes
    mov r8d, [rbp-64]
    dec r8d                              ; last row index

.flip_row:
    test r8d, r8d
    js .store_sprite

    ; dest row = pixel_buf + row_index * row_bytes
    mov eax, [rbp-56]
    shl eax, 2
    push rdi
    imul r9d, r8d, 1
    imul r9d, eax
    movsxd r9, r9d
    lea rdi, [rdi + r9]                 ; no, recalc

    pop rdi
    mov eax, [rbp-56]
    shl eax, 2
    mov r10d, [rbp-64]
    dec r10d
    sub r10d, r8d                        ; dest_row = (height-1) - src_row
    imul r10d, eax
    movsxd r10, r10d

    ; Simple approach: copy row by row
    mov r11d, [rbp-56]                  ; pixels per row
.copy_pixel:
    test r11d, r11d
    jz .next_flip_row
    mov eax, [rsi]                       ; read BGRA (or BGR)
    mov [rdi + r10], eax
    add rsi, 4
    add r10d, 4
    dec r11d
    jmp .copy_pixel

.next_flip_row:
    dec r8d
    jmp .flip_row

.top_down_copy:
    ; Already top-down, straight copy
    rep movsd
    jmp .store_sprite

.store_sprite:
    ; Store sprite descriptor
    lea rdi, [rel g_engine]
    mov rax, [rdi + EngineState.sprites]
    mov ecx, [rbp-12]                   ; slot index
    imul ecx, SpriteDesc_size
    movsxd rcx, ecx
    add rax, rcx                        ; sprite descriptor ptr

    mov rcx, [rbp-80]
    mov [rax + SpriteDesc.pixels], rcx
    mov ecx, [rbp-56]
    mov [rax + SpriteDesc.width], ecx
    mov ecx, [rbp-64]
    mov [rax + SpriteDesc.height], ecx
    mov ecx, [rbp-56]
    shl ecx, 2
    mov [rax + SpriteDesc.pitch], ecx
    mov dword [rax + SpriteDesc.flags], 1 ; has alpha

    inc dword [rdi + EngineState.sprite_count]
    mov eax, [rbp-12]                   ; return slot index
    jmp .done

.close_error:
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
; ae_draw_sprite(id: ecx, x: edx, y: r8d, flags: r9d)
; Draws sprite at position with optional alpha blending
; flags: bit 0 = alpha blend
; ============================================================================
ae_draw_sprite:
    SAVE_NONVOL
    push rbp
    mov rbp, rsp
    sub rsp, 80

    lea rdi, [rel g_engine]

    ; Validate sprite ID
    test ecx, ecx
    js .sprite_done
    cmp ecx, [rdi + EngineState.sprite_count]
    jge .sprite_done

    mov [rbp-4], edx                    ; dest x
    mov [rbp-8], r8d                    ; dest y
    mov [rbp-12], r9d                   ; flags

    ; Get sprite descriptor
    mov rax, [rdi + EngineState.sprites]
    imul ecx, SpriteDesc_size
    movsxd rcx, ecx
    add rax, rcx
    mov [rbp-24], rax                   ; sprite desc ptr

    mov r12, [rax + SpriteDesc.pixels]  ; src pixels
    mov r13d, [rax + SpriteDesc.width]  ; sprite width
    mov r14d, [rax + SpriteDesc.height] ; sprite height
    mov r15d, [rax + SpriteDesc.pitch]  ; sprite pitch

    mov rbx, [rdi + EngineState.framebuffer]
    mov esi, [rdi + EngineState.stride]

    ; Row loop
    xor ecx, ecx                         ; row = 0
.row:
    cmp ecx, r14d
    jge .sprite_done

    mov eax, [rbp-8]
    add eax, ecx                        ; dest_y = y + row
    test eax, eax
    js .next_row
    cmp eax, [rdi + EngineState.height]
    jge .sprite_done                     ; past bottom = done

    ; Calculate dest row start
    imul eax, esi                        ; dest_y * stride
    mov [rbp-28], eax

    ; Calculate src row start
    mov eax, ecx
    imul eax, r15d                       ; row * sprite_pitch
    mov [rbp-32], eax

    ; Column loop
    xor edx, edx                         ; col = 0
.col:
    cmp edx, r13d
    jge .next_row

    mov eax, [rbp-4]
    add eax, edx                        ; dest_x = x + col
    test eax, eax
    js .next_col
    cmp eax, [rdi + EngineState.width]
    jge .next_row                        ; past right = next row

    ; Source pixel offset
    mov r8d, [rbp-32]
    lea r8d, [r8d + edx*4]
    movsxd r8, r8d
    mov r9d, [r12 + r8]                 ; source BGRA pixel

    ; Check alpha blending flag
    test dword [rbp-12], 1
    jz .no_blend

    ; Get alpha
    mov r10d, r9d
    shr r10d, 24
    and r10d, 0xFF
    test r10d, r10d
    jz .next_col                         ; fully transparent, skip
    cmp r10d, 255
    je .no_blend                         ; fully opaque, no blend needed

    ; Alpha blend: dest = (src * alpha + dest * (255 - alpha)) / 255
    ; Calculate dest offset
    mov r11d, [rbp-28]
    lea r11d, [r11d + eax*4]
    movsxd r11, r11d
    mov eax, [rbx + r11]                ; dest pixel

    ; Blend each channel (simplified)
    ; For now, just do source-over with alpha threshold
    cmp r10d, 128
    jl .next_col                         ; skip if < 50% alpha

.no_blend:
    ; Write pixel directly
    mov r11d, [rbp-28]
    mov eax, [rbp-4]
    add eax, edx
    lea r11d, [r11d + eax*4]
    movsxd r11, r11d
    mov [rbx + r11], r9d

.next_col:
    inc edx
    jmp .col

.next_row:
    inc ecx
    jmp .row

.sprite_done:
    mov rsp, rbp
    pop rbp
    RESTORE_NONVOL
    ret
