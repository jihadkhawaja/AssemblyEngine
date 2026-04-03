; ============================================================================
; AssemblyEngine - Platform Layer (Windows x64)
; Creates window, runs message loop, manages engine lifecycle
; Target: x86-64 Windows (NASM)
; ============================================================================

%include "include/engine_state.inc"
%include "include/win64api.inc"

global ae_init
global ae_shutdown
global ae_poll_events
global ae_present
global g_engine
global ae_wndproc

section .data
    class_name  db "AE_WindowClass", 0
    def_title   db "AssemblyEngine", 0

section .bss
    g_engine    resb EngineState_size

section .text

; ============================================================================
; ae_init(width: ecx, height: edx, title: r8) -> rax (1=ok, 0=fail)
; Initializes engine: creates window, framebuffer, subsystems
; ============================================================================
ae_init:
    SAVE_NONVOL
    push rbp
    mov rbp, rsp
    sub rsp, 264                        ; locals + shadow + alignment

    ; Store parameters
    mov [rbp-8], ecx                    ; width
    mov [rbp-12], edx                   ; height
    mov [rbp-24], r8                    ; title (may be null)

    ; Get module handle
    xor rcx, rcx
    call GetModuleHandleA
    lea rdi, [rel g_engine]
    mov [rdi + EngineState.hinstance], rax

    ; Store dimensions
    mov ecx, [rbp-8]
    mov edx, [rbp-12]
    mov [rdi + EngineState.width], ecx
    mov [rdi + EngineState.height], edx
    imul ecx, AE_PIXEL_SIZE
    mov [rdi + EngineState.stride], ecx

    ; Setup BITMAPINFOHEADER
    mov dword [rdi + EngineState.bmi_size], 40
    mov ecx, [rbp-8]
    mov [rdi + EngineState.bmi_width], ecx
    mov ecx, [rbp-12]
    neg ecx                             ; negative = top-down DIB
    mov [rdi + EngineState.bmi_height], ecx
    mov word [rdi + EngineState.bmi_planes], 1
    mov word [rdi + EngineState.bmi_bitcount], 32
    mov dword [rdi + EngineState.bmi_compress], BI_RGB

    ; Load cursor
    xor rcx, rcx
    mov rdx, IDC_ARROW
    call LoadCursorA
    mov [rbp-32], rax                   ; save cursor handle

    ; Register window class (WNDCLASSEX on stack)
    lea rbx, [rbp-160]                  ; WNDCLASSEX buffer
    xor eax, eax
    mov ecx, WNDCLASSEX_SIZE
    lea rdi, [rbx]
    rep stosb                           ; zero-fill

    lea rdi, [rel g_engine]             ; restore rdi
    mov dword [rbx], WNDCLASSEX_SIZE    ; cbSize
    mov dword [rbx+4], 0x0023          ; style = CS_HREDRAW|CS_VREDRAW|CS_OWNDC
    lea rax, [rel ae_wndproc]
    mov [rbx+8], rax                    ; lpfnWndProc
    mov rax, [rdi + EngineState.hinstance]
    mov [rbx+24], rax                   ; hInstance
    mov rax, [rbp-32]
    mov [rbx+40], rax                   ; hCursor
    lea rax, [rel class_name]
    mov [rbx+64], rax                   ; lpszClassName

    mov rcx, rbx
    call RegisterClassExA
    test ax, ax
    jz .fail

    ; Calculate adjusted window rect
    lea rbx, [rbp-176]                  ; RECT on stack
    mov dword [rbx], 0                  ; left
    mov dword [rbx+4], 0               ; top
    mov eax, [rdi + EngineState.width]
    mov [rbx+8], eax                    ; right
    mov eax, [rdi + EngineState.height]
    mov [rbx+12], eax                   ; bottom

    mov rcx, rbx
    mov edx, WS_OVERLAPPEDWINDOW
    xor r8d, r8d                        ; no menu
    xor r9d, r9d                        ; not extended style adjustment
    call AdjustWindowRectEx

    ; Compute actual window size
    mov eax, [rbx+8]
    sub eax, [rbx]                      ; width = right - left
    mov [rbp-40], eax
    mov eax, [rbx+12]
    sub eax, [rbx+4]                    ; height = bottom - top
    mov [rbp-44], eax

    ; Create window
    mov ecx, WS_EX_APPWINDOW           ; dwExStyle
    lea rdx, [rel class_name]           ; lpClassName

    ; Choose title
    mov r8, [rbp-24]
    test r8, r8
    jnz .has_title
    lea r8, [rel def_title]
.has_title:
    mov r9d, WS_OVERLAPPEDWINDOW       ; dwStyle

    ; Pass remaining CreateWindowExA arguments in the caller stack area.
    sub rsp, 96                         ; shadow space + 8 stack args
    mov qword [rsp+32], 0              ; x = CW_USEDEFAULT
    mov dword [rsp+32], 0x80000000     ; CW_USEDEFAULT
    mov qword [rsp+40], 0
    mov dword [rsp+40], 0x80000000     ; y = CW_USEDEFAULT
    mov eax, [rbp-40]
    mov [rsp+48], eax                   ; nWidth
    mov eax, [rbp-44]
    mov [rsp+56], eax                   ; nHeight
    mov qword [rsp+64], 0             ; hWndParent
    mov qword [rsp+72], 0             ; hMenu
    mov rax, [rdi + EngineState.hinstance]
    mov [rsp+80], rax                   ; hInstance
    mov qword [rsp+88], 0             ; lpParam

    call CreateWindowExA
    add rsp, 96
    test rax, rax
    jz .fail

    mov [rdi + EngineState.hwnd], rax

    ; Show window
    mov rcx, rax
    mov edx, SW_SHOW
    call ShowWindow

    mov rcx, [rdi + EngineState.hwnd]
    call UpdateWindow

    ; Get DC
    mov rcx, [rdi + EngineState.hwnd]
    call GetDC
    mov [rdi + EngineState.hdc], rax

    ; Create memory DC and DIB section
    mov rcx, rax
    call CreateCompatibleDC
    mov [rdi + EngineState.mem_dc], rax

    lea rcx, [rdi + EngineState.bmi_size] ; pBitmapInfo
    mov rdx, [rdi + EngineState.hdc]
    ; CreateDIBSection(hdc, pBMI, usage, ppvBits, hSection, offset)
    mov rcx, rdx                        ; hdc
    lea rdx, [rdi + EngineState.bmi_size]
    mov r8d, DIB_RGB_COLORS
    lea r9, [rdi + EngineState.framebuffer]
    sub rsp, 48
    mov qword [rsp+32], 0               ; hSection
    mov qword [rsp+40], 0               ; dwOffset
    call CreateDIBSection
    add rsp, 48
    mov [rdi + EngineState.hbitmap], rax

    ; Select bitmap into memory DC
    mov rcx, [rdi + EngineState.mem_dc]
    mov rdx, rax
    call SelectObject

    ; Mark engine as running
    mov dword [rdi + EngineState.running], 1

    ; Initialize timer subsystem
    extern ae_timer_init
    call ae_timer_init

    ; Initialize memory subsystem
    extern ae_memory_init
    call ae_memory_init

    mov eax, 1
    jmp .done

.fail:
    xor eax, eax

.done:
    mov rsp, rbp
    pop rbp
    RESTORE_NONVOL
    ret

; ============================================================================
; ae_shutdown() - Cleans up all engine resources
; ============================================================================
ae_shutdown:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 40

    lea rdi, [rel g_engine]

    ; Delete GDI objects
    mov rcx, [rdi + EngineState.hbitmap]
    test rcx, rcx
    jz .no_bitmap
    call DeleteObject
.no_bitmap:

    mov rcx, [rdi + EngineState.mem_dc]
    test rcx, rcx
    jz .no_memdc
    call DeleteDC
.no_memdc:

    ; Release DC
    mov rcx, [rdi + EngineState.hwnd]
    mov rdx, [rdi + EngineState.hdc]
    test rcx, rcx
    jz .no_dc
    call ReleaseDC
.no_dc:

    ; Destroy window
    mov rcx, [rdi + EngineState.hwnd]
    test rcx, rcx
    jz .no_wnd
    call DestroyWindow
.no_wnd:

    ; Free memory arena
    extern ae_memory_shutdown
    call ae_memory_shutdown

    mov dword [rdi + EngineState.running], 0

    add rsp, 40
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_poll_events() -> eax (1=running, 0=quit)
; Processes all pending Windows messages
; ============================================================================
ae_poll_events:
    push rbp
    mov rbp, rsp
    push rdi
    push rsi
    sub rsp, 96                         ; MSG + shadow

    lea rdi, [rel g_engine]

    ; Copy current keys to prev_keys
    lea rsi, [rdi + EngineState.keys]
    lea rdx, [rdi + EngineState.prev_keys]
    mov ecx, 256
.copy_keys:
    mov al, [rsi + rcx - 1]
    mov [rdx + rcx - 1], al
    dec ecx
    jnz .copy_keys

    ; Copy mouse buttons
    mov eax, [rdi + EngineState.mouse_buttons]
    mov [rdi + EngineState.prev_mouse_btn], eax

    ; Update timer
    extern ae_timer_update
    call ae_timer_update

.msg_loop:
    lea rcx, [rbp-64]                  ; lpMsg
    xor edx, edx                        ; hWnd = NULL (all)
    xor r8d, r8d                        ; wMsgFilterMin
    xor r9d, r9d                        ; wMsgFilterMax
    sub rsp, 48
    mov dword [rsp+32], PM_REMOVE
    call PeekMessageA
    add rsp, 48
    test eax, eax
    jz .msg_done

    ; Check for WM_QUIT
    mov eax, [rbp-64+8]                ; msg.message offset
    cmp eax, WM_QUIT
    je .quit

    lea rcx, [rbp-64]
    call TranslateMessage
    lea rcx, [rbp-64]
    call DispatchMessageA
    jmp .msg_loop

.quit:
    lea rdi, [rel g_engine]
    mov dword [rdi + EngineState.running], 0

.msg_done:
    lea rdi, [rel g_engine]
    mov eax, [rdi + EngineState.running]

    add rsp, 96
    pop rsi
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_present() - Blits framebuffer to window
; ============================================================================
ae_present:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 72

    lea rdi, [rel g_engine]

    mov rcx, [rdi + EngineState.hdc]    ; hdcDest
    xor edx, edx                         ; xDest
    xor r8d, r8d                         ; yDest
    mov r9d, [rdi + EngineState.width]   ; nWidth
    mov eax, [rdi + EngineState.height]
    mov [rsp+32], eax                    ; nHeight
    mov qword [rsp+40], 0
    mov rax, [rdi + EngineState.mem_dc]
    mov [rsp+40], rax                    ; hdcSrc
    mov qword [rsp+48], 0                ; xSrc
    mov qword [rsp+56], 0                ; ySrc
    mov dword [rsp+64], SRCCOPY          ; dwRop
    call BitBlt

    add rsp, 72
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_wndproc(hwnd: rcx, msg: edx, wparam: r8, lparam: r9) -> rax
; Window procedure - handles input messages
; ============================================================================
ae_wndproc:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 72

    ; Save params
    mov [rbp-8], rcx                    ; hwnd
    mov [rbp-12], edx                   ; msg
    mov [rbp-24], r8                    ; wparam
    mov [rbp-32], r9                    ; lparam

    cmp edx, WM_CLOSE
    je .on_close
    cmp edx, WM_DESTROY
    je .on_destroy
    cmp edx, WM_KEYDOWN
    je .on_keydown
    cmp edx, WM_KEYUP
    je .on_keyup
    cmp edx, WM_MOUSEMOVE
    je .on_mousemove
    cmp edx, WM_LBUTTONDOWN
    je .on_lbuttondown
    cmp edx, WM_LBUTTONUP
    je .on_lbuttonup
    cmp edx, WM_RBUTTONDOWN
    je .on_rbuttondown
    cmp edx, WM_RBUTTONUP
    je .on_rbuttonup
    jmp .default

.on_close:
    lea rdi, [rel g_engine]
    mov dword [rdi + EngineState.running], 0
    mov rcx, [rbp-8]
    call DestroyWindow
    xor eax, eax
    jmp .done

.on_destroy:
    xor ecx, ecx
    call PostQuitMessage
    xor eax, eax
    jmp .done

.on_keydown:
    lea rdi, [rel g_engine]
    mov rax, [rbp-24]                   ; wparam = virtual key code
    and eax, 0xFF
    mov byte [rdi + EngineState.keys + rax], 1
    xor eax, eax
    jmp .done

.on_keyup:
    lea rdi, [rel g_engine]
    mov rax, [rbp-24]
    and eax, 0xFF
    mov byte [rdi + EngineState.keys + rax], 0
    xor eax, eax
    jmp .done

.on_mousemove:
    lea rdi, [rel g_engine]
    mov rax, [rbp-32]                   ; lparam
    movsx ecx, ax                        ; x = LOWORD(lparam)
    shr eax, 16
    movsx edx, ax                        ; y = HIWORD(lparam)
    mov [rdi + EngineState.mouse_x], ecx
    mov [rdi + EngineState.mouse_y], edx
    xor eax, eax
    jmp .done

.on_lbuttondown:
    lea rdi, [rel g_engine]
    or dword [rdi + EngineState.mouse_buttons], 1
    xor eax, eax
    jmp .done

.on_lbuttonup:
    lea rdi, [rel g_engine]
    and dword [rdi + EngineState.mouse_buttons], ~1
    xor eax, eax
    jmp .done

.on_rbuttondown:
    lea rdi, [rel g_engine]
    or dword [rdi + EngineState.mouse_buttons], 2
    xor eax, eax
    jmp .done

.on_rbuttonup:
    lea rdi, [rel g_engine]
    and dword [rdi + EngineState.mouse_buttons], ~2
    xor eax, eax
    jmp .done

.default:
    mov rcx, [rbp-8]
    mov edx, [rbp-12]
    mov r8, [rbp-24]
    mov r9, [rbp-32]
    call DefWindowProcA

.done:
    add rsp, 72
    pop rdi
    pop rbp
    ret
