; ============================================================================
; AssemblyEngine - 2D Software Renderer
; Framebuffer operations: clear, pixels, rectangles, lines, circles
; Target: x86-64 Windows (NASM)
; ============================================================================

%include "include/engine_state.inc"

extern g_engine

global ae_clear
global ae_draw_pixel
global ae_draw_rect
global ae_draw_filled_rect
global ae_draw_line
global ae_draw_circle

section .text

; ============================================================================
; ae_clear(r: ecx, g: edx, b: r8d, a: r9d)
; Fills entire framebuffer with color using REP STOSD
; ============================================================================
ae_clear:
    push rbp
    mov rbp, rsp
    push rsi
    push rdi

    ; Build BGRA pixel value
    and ecx, 0xFF
    and edx, 0xFF
    and r8d, 0xFF
    and r9d, 0xFF
    shl r9d, 24                         ; A << 24
    shl ecx, 16                         ; R << 16
    shl edx, 8                          ; G << 8
    or ecx, edx
    or ecx, r8d                          ; B
    or ecx, r9d                          ; ARGB -> but framebuffer is BGRA
    mov eax, ecx

    lea rsi, [rel g_engine]
    mov rdi, [rsi + EngineState.framebuffer]
    test rdi, rdi
    jz .done

    mov ecx, [rsi + EngineState.width]
    imul ecx, [rsi + EngineState.height]
    rep stosd

.done:
    pop rdi
    pop rsi
    mov rsp, rbp
    pop rbp
    ret

; ============================================================================
; ae_draw_pixel(x: ecx, y: edx, r: r8d, g: r9d)
; Stack: b, a
; Draws a single pixel with bounds checking
; ============================================================================
ae_draw_pixel:
    push rbp
    mov rbp, rsp
    push rsi

    lea rax, [rel g_engine]

    ; Bounds check
    test ecx, ecx
    js .skip
    test edx, edx
    js .skip
    cmp ecx, [rax + EngineState.width]
    jge .skip
    cmp edx, [rax + EngineState.height]
    jge .skip

    ; Get extra params from stack
    mov r10d, [rbp+48]                  ; b (after shadow + return + rbp)
    mov r11d, [rbp+56]                  ; a

    ; Build color: BGRA in memory = B, G, R, A
    and r10d, 0xFF                       ; B
    and r9d, 0xFF                        ; G
    shl r9d, 8
    or r10d, r9d
    and r8d, 0xFF                        ; R
    shl r8d, 16
    or r10d, r8d
    and r11d, 0xFF                       ; A
    shl r11d, 24
    or r10d, r11d

    ; Calculate offset: (y * stride) + (x * 4)
    mov r11d, [rax + EngineState.stride]
    imul edx, r11d
    lea edx, [edx + ecx*4]
    mov rsi, [rax + EngineState.framebuffer]
    mov [rsi + rdx], r10d

.skip:
    pop rsi
    mov rsp, rbp
    pop rbp
    ret

; ============================================================================
; ae_draw_rect(x: ecx, y: edx, w: r8d, h: r9d)
; Stack: r, g, b, a
; Draws an outlined rectangle
; ============================================================================
ae_draw_rect:
    SAVE_NONVOL
    push rbp
    mov rbp, rsp
    sub rsp, 64

    mov [rbp-4], ecx                    ; x
    mov [rbp-8], edx                    ; y
    mov [rbp-12], r8d                   ; w
    mov [rbp-16], r9d                   ; h

    ; Calculate adjusted stack offset for params (7 pushes + push rbp = 64)
    mov eax, [rbp+104]                  ; r
    mov [rbp-20], eax
    mov eax, [rbp+112]                  ; g
    mov [rbp-24], eax
    mov eax, [rbp+120]                  ; b
    mov [rbp-28], eax
    mov eax, [rbp+128]                  ; a
    mov [rbp-32], eax

    ; Top edge: horizontal line at y from x to x+w-1
    mov ecx, [rbp-4]                    ; x1
    mov edx, [rbp-8]                    ; y1
    mov r8d, ecx
    add r8d, [rbp-12]
    dec r8d                              ; x2 = x+w-1
    mov r9d, edx                         ; y2 = y (horizontal)
    sub rsp, 64
    mov eax, [rbp-20]
    mov [rsp+32], eax                   ; r
    mov eax, [rbp-24]
    mov [rsp+40], eax                   ; g
    mov eax, [rbp-28]
    mov [rsp+48], eax                   ; b
    mov eax, [rbp-32]
    mov [rsp+56], eax                   ; a
    call ae_draw_line
    add rsp, 64

    ; Bottom edge
    mov ecx, [rbp-4]
    mov edx, [rbp-8]
    add edx, [rbp-16]
    dec edx                              ; y+h-1
    mov r8d, ecx
    add r8d, [rbp-12]
    dec r8d
    mov r9d, edx
    sub rsp, 64
    mov eax, [rbp-20]
    mov [rsp+32], eax
    mov eax, [rbp-24]
    mov [rsp+40], eax
    mov eax, [rbp-28]
    mov [rsp+48], eax
    mov eax, [rbp-32]
    mov [rsp+56], eax
    call ae_draw_line
    add rsp, 64

    ; Left edge
    mov ecx, [rbp-4]
    mov edx, [rbp-8]
    mov r8d, ecx
    mov r9d, edx
    add r9d, [rbp-16]
    dec r9d
    sub rsp, 64
    mov eax, [rbp-20]
    mov [rsp+32], eax
    mov eax, [rbp-24]
    mov [rsp+40], eax
    mov eax, [rbp-28]
    mov [rsp+48], eax
    mov eax, [rbp-32]
    mov [rsp+56], eax
    call ae_draw_line
    add rsp, 64

    ; Right edge
    mov ecx, [rbp-4]
    add ecx, [rbp-12]
    dec ecx
    mov edx, [rbp-8]
    mov r8d, ecx
    mov r9d, edx
    add r9d, [rbp-16]
    dec r9d
    sub rsp, 64
    mov eax, [rbp-20]
    mov [rsp+32], eax
    mov eax, [rbp-24]
    mov [rsp+40], eax
    mov eax, [rbp-28]
    mov [rsp+48], eax
    mov eax, [rbp-32]
    mov [rsp+56], eax
    call ae_draw_line
    add rsp, 64

    mov rsp, rbp
    pop rbp
    RESTORE_NONVOL
    ret

; ============================================================================
; ae_draw_filled_rect(x: ecx, y: edx, w: r8d, h: r9d)
; Stack: r, g, b, a
; Draws a filled rectangle with clipping
; ============================================================================
ae_draw_filled_rect:
    SAVE_NONVOL
    push rbp
    mov rbp, rsp
    sub rsp, 64

    lea rax, [rel g_engine]

    ; Clamp coordinates to framebuffer bounds
    ; x1 = max(x, 0)
    test ecx, ecx
    cmovs ecx, [rsp]                    ; if negative, 0
    xor esi, esi
    test ecx, ecx
    cmovs ecx, esi

    test edx, edx
    cmovs edx, esi

    ; x2 = min(x+w, width)
    mov r10d, ecx
    add r10d, r8d
    cmp r10d, [rax + EngineState.width]
    jle .w_ok
    mov r10d, [rax + EngineState.width]
.w_ok:
    ; y2 = min(y+h, height)
    mov r11d, edx
    add r11d, r9d
    cmp r11d, [rax + EngineState.height]
    jle .h_ok
    mov r11d, [rax + EngineState.height]
.h_ok:
    ; Store loop bounds
    mov [rbp-4], ecx                    ; x1
    mov [rbp-8], edx                    ; y1
    mov [rbp-12], r10d                  ; x2
    mov [rbp-16], r11d                  ; y2

    ; Build BGRA color
    mov eax, [rbp+104]                  ; r
    mov r8d, [rbp+112]                  ; g
    mov r9d, [rbp+120]                  ; b
    mov r10d, [rbp+128]                 ; a
    and r9d, 0xFF                        ; B
    and r8d, 0xFF
    shl r8d, 8                           ; G
    or r9d, r8d
    and eax, 0xFF
    shl eax, 16                          ; R
    or r9d, eax
    and r10d, 0xFF
    shl r10d, 24                         ; A
    or r9d, r10d
    mov [rbp-20], r9d                    ; packed color

    lea rax, [rel g_engine]
    mov rdi, [rax + EngineState.framebuffer]
    mov r12d, [rax + EngineState.stride]

    ; Outer loop: rows
    mov r13d, [rbp-8]                   ; y = y1
.row_loop:
    cmp r13d, [rbp-16]                  ; y < y2?
    jge .row_done

    ; Calculate row start: framebuffer + y * stride + x1 * 4
    mov eax, r13d
    imul eax, r12d
    mov ecx, [rbp-4]
    lea eax, [eax + ecx*4]
    lea rsi, [rdi + rax]

    ; Fill row with color using REP STOSD
    mov ecx, [rbp-12]
    sub ecx, [rbp-4]                    ; pixel count = x2 - x1
    test ecx, ecx
    jle .next_row

    push rdi
    mov rdi, rsi
    mov eax, [rbp-20]
    rep stosd
    pop rdi

.next_row:
    inc r13d
    jmp .row_loop

.row_done:
    mov rsp, rbp
    pop rbp
    RESTORE_NONVOL
    ret

; ============================================================================
; ae_draw_line(x1: ecx, y1: edx, x2: r8d, y2: r9d)
; Stack: r, g, b, a
; Bresenham's line algorithm
; ============================================================================
ae_draw_line:
    SAVE_NONVOL
    push rbp
    mov rbp, rsp
    sub rsp, 80

    mov [rbp-4], ecx                    ; x1 (current x)
    mov [rbp-8], edx                    ; y1 (current y)
    mov [rbp-12], r8d                   ; x2
    mov [rbp-16], r9d                   ; y2

    ; Build color from stack params
    mov eax, [rbp+104]                  ; r
    mov r8d, [rbp+112]                  ; g
    mov r9d, [rbp+120]                  ; b
    mov r10d, [rbp+128]                 ; a
    and r9d, 0xFF
    and r8d, 0xFF
    shl r8d, 8
    or r9d, r8d
    and eax, 0xFF
    shl eax, 16
    or r9d, eax
    and r10d, 0xFF
    shl r10d, 24
    or r9d, r10d
    mov [rbp-20], r9d                    ; packed color

    ; Calculate dx, dy, sx, sy
    mov eax, [rbp-12]
    sub eax, [rbp-4]                    ; dx = x2 - x1
    mov r12d, 1                          ; sx = 1
    test eax, eax
    jns .dx_pos
    neg eax
    mov r12d, -1                         ; sx = -1
.dx_pos:
    mov [rbp-24], eax                    ; abs(dx)

    mov eax, [rbp-16]
    sub eax, [rbp-8]                    ; dy = y2 - y1
    mov r13d, 1                          ; sy = 1
    test eax, eax
    jns .dy_pos
    neg eax
    mov r13d, -1                         ; sy = -1
.dy_pos:
    mov [rbp-28], eax                    ; abs(dy)

    ; err = dx - dy
    mov eax, [rbp-24]
    sub eax, [rbp-28]
    mov [rbp-32], eax                    ; err

    lea rbx, [rel g_engine]
    mov r14, [rbx + EngineState.framebuffer]
    mov r15d, [rbx + EngineState.stride]

.line_loop:
    ; Plot pixel at (x, y) with bounds check
    mov ecx, [rbp-4]
    mov edx, [rbp-8]
    test ecx, ecx
    js .line_skip
    test edx, edx
    js .line_skip
    cmp ecx, [rbx + EngineState.width]
    jge .line_skip
    cmp edx, [rbx + EngineState.height]
    jge .line_skip

    ; Write pixel directly
    imul edx, r15d
    lea edx, [edx + ecx*4]
    mov eax, [rbp-20]
    mov [r14 + rdx], eax

.line_skip:
    ; Check if we reached endpoint
    mov ecx, [rbp-4]
    cmp ecx, [rbp-12]
    jne .line_cont
    mov edx, [rbp-8]
    cmp edx, [rbp-16]
    je .line_done

.line_cont:
    ; e2 = 2 * err
    mov eax, [rbp-32]
    shl eax, 1                           ; e2

    ; if e2 > -dy: err -= dy, x += sx
    mov ecx, [rbp-28]
    neg ecx                              ; -dy
    cmp eax, ecx
    jle .no_x_step
    mov ecx, [rbp-28]
    sub [rbp-32], ecx
    add [rbp-4], r12d
.no_x_step:

    ; Recalculate e2 (may have changed)
    mov eax, [rbp-32]
    shl eax, 1

    ; if e2 < dx: err += dx, y += sy
    cmp eax, [rbp-24]
    jge .no_y_step
    mov ecx, [rbp-24]
    add [rbp-32], ecx
    add [rbp-8], r13d
.no_y_step:

    jmp .line_loop

.line_done:
    mov rsp, rbp
    pop rbp
    RESTORE_NONVOL
    ret

; ============================================================================
; ae_draw_circle(cx: ecx, cy: edx, radius: r8d, r: r9d)
; Stack: g, b, a
; Midpoint circle algorithm
; ============================================================================
ae_draw_circle:
    SAVE_NONVOL
    push rbp
    mov rbp, rsp
    sub rsp, 80

    mov [rbp-4], ecx                    ; cx
    mov [rbp-8], edx                    ; cy
    mov [rbp-12], r8d                   ; radius

    ; Build color
    and r9d, 0xFF                        ; r
    shl r9d, 16
    mov eax, [rbp+104]                  ; g
    and eax, 0xFF
    shl eax, 8
    or r9d, eax
    mov eax, [rbp+112]                  ; b
    and eax, 0xFF
    or r9d, eax
    mov eax, [rbp+120]                  ; a
    and eax, 0xFF
    shl eax, 24
    or r9d, eax
    mov [rbp-16], r9d                    ; packed BGRA

    lea rbx, [rel g_engine]
    mov r14, [rbx + EngineState.framebuffer]
    mov r15d, [rbx + EngineState.stride]

    ; Midpoint circle: x=radius, y=0, d=1-radius
    mov r12d, [rbp-12]                  ; x = radius
    xor r13d, r13d                       ; y = 0
    mov eax, 1
    sub eax, r12d
    mov [rbp-20], eax                    ; d = 1 - radius

.circle_loop:
    cmp r13d, r12d
    jg .circle_done

    ; Plot 8 symmetric points using macro-like inline
    ; Helper: plot pixel at (cx+ox, cy+oy)
    %macro PLOT_POINT 2                  ; %1=x_offset_reg, %2=y_offset_reg
        mov ecx, [rbp-4]
        add ecx, %1
        mov edx, [rbp-8]
        add edx, %2
        ; Bounds check
        test ecx, ecx
        js %%skip
        test edx, edx
        js %%skip
        cmp ecx, [rbx + EngineState.width]
        jge %%skip
        cmp edx, [rbx + EngineState.height]
        jge %%skip
        imul edx, r15d
        lea edx, [edx + ecx*4]
        mov eax, [rbp-16]
        mov [r14 + rdx], eax
    %%skip:
    %endmacro

    PLOT_POINT r12d, r13d               ; ( x,  y)
    PLOT_POINT r13d, r12d               ; ( y,  x)
    mov esi, r13d
    neg esi
    PLOT_POINT esi, r12d                ; (-y,  x)
    mov esi, r12d
    neg esi
    PLOT_POINT esi, r13d                ; (-x,  y)
    mov esi, r12d
    neg esi
    PLOT_POINT esi, r13d                ; (-x,  y) - already done, need (-x, -y)
    mov esi, r12d
    neg esi
    mov edi, r13d
    neg edi
    PLOT_POINT esi, edi                 ; (-x, -y)
    mov esi, r13d
    neg esi
    mov edi, r12d
    neg edi
    PLOT_POINT esi, edi                 ; (-y, -x)
    mov edi, r12d
    neg edi
    PLOT_POINT r13d, edi                ; ( y, -x)
    mov edi, r13d
    neg edi
    PLOT_POINT r12d, edi                ; ( x, -y)

    ; Update decision parameter
    inc r13d
    cmp dword [rbp-20], 0
    jg .d_positive
    ; d <= 0: d += 2*y + 1
    mov eax, r13d
    shl eax, 1
    inc eax
    add [rbp-20], eax
    jmp .circle_loop

.d_positive:
    dec r12d
    ; d += 2*(y - x) + 1
    mov eax, r13d
    sub eax, r12d
    shl eax, 1
    inc eax
    add [rbp-20], eax
    jmp .circle_loop

.circle_done:
    mov rsp, rbp
    pop rbp
    RESTORE_NONVOL
    ret
