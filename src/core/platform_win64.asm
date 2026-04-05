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
global ae_set_vsync_enabled
global ae_resize_window
global ae_set_window_mode
global ae_get_window_mode
global ae_get_window_width
global ae_get_window_height
global ae_get_window_handle
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
    mov dword [rdi + EngineState.vsync_enabled], 1
    mov dword [rdi + EngineState.window_mode], AE_WINDOWMODE_WINDOWED
    mov dword [rdi + EngineState.restore_valid], 0

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

    cmp dword [rdi + EngineState.vsync_enabled], 0
    je .done_present
    call DwmFlush

.done_present:

    add rsp, 72
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_set_vsync_enabled(enabled: ecx)
; Enables or disables compositor-synced presents.
; ============================================================================
ae_set_vsync_enabled:
    lea rax, [rel g_engine]
    xor edx, edx
    test ecx, ecx
    setne dl
    mov [rax + EngineState.vsync_enabled], edx
    ret

; ============================================================================
; ae_get_window_width() -> eax
; Returns the current client width tracked by the native platform layer.
; ============================================================================
ae_get_window_width:
    lea rax, [rel g_engine]
    mov eax, [rax + EngineState.width]
    ret

; ============================================================================
; ae_get_window_height() -> eax
; Returns the current client height tracked by the native platform layer.
; ============================================================================
ae_get_window_height:
    lea rax, [rel g_engine]
    mov eax, [rax + EngineState.height]
    ret

; ============================================================================
; ae_get_window_handle() -> rax
; Returns the native HWND used for presentation and external graphics APIs.
; ============================================================================
ae_get_window_handle:
    lea rax, [rel g_engine]
    mov rax, [rax + EngineState.hwnd]
    ret

; ============================================================================
; ae_get_window_mode() -> eax
; Returns the current native window mode.
; ============================================================================
ae_get_window_mode:
    lea rax, [rel g_engine]
    mov eax, [rax + EngineState.window_mode]
    ret

; ============================================================================
; ae_apply_client_resize(width: ecx, height: edx) -> eax (1=ok, 0=fail)
; Recreates the backbuffer for an already-known client size.
; ============================================================================
ae_apply_client_resize:
    push rbp
    mov rbp, rsp
    push rdi
    push rbx
    sub rsp, 128

    cmp ecx, 1
    jl .resize_fail
    cmp edx, 1
    jl .resize_fail

    mov [rbp-4], ecx
    mov [rbp-8], edx

    lea rdi, [rel g_engine]

    mov eax, [rdi + EngineState.width]
    cmp eax, [rbp-4]
    jne .check_dc
    mov eax, [rdi + EngineState.height]
    cmp eax, [rbp-8]
    jne .check_dc
    mov rax, [rdi + EngineState.hbitmap]
    test rax, rax
    jnz .update_state_only

.check_dc:
    mov rax, [rdi + EngineState.hdc]
    test rax, rax
    jz .update_state_only
    mov rax, [rdi + EngineState.mem_dc]
    test rax, rax
    jz .update_state_only

    mov dword [rbp-96], 40
    mov eax, [rbp-4]
    mov [rbp-92], eax
    mov eax, [rbp-8]
    neg eax
    mov [rbp-88], eax
    mov word [rbp-84], 1
    mov word [rbp-82], 32
    mov dword [rbp-80], BI_RGB
    mov eax, [rbp-4]
    imul eax, [rbp-8]
    imul eax, AE_PIXEL_SIZE
    mov [rbp-76], eax
    mov dword [rbp-72], 0
    mov dword [rbp-68], 0
    mov dword [rbp-64], 0
    mov dword [rbp-60], 0
    mov dword [rbp-56], 0

    mov rcx, [rdi + EngineState.hdc]
    lea rdx, [rbp-96]
    mov r8d, DIB_RGB_COLORS
    lea r9, [rbp-24]
    sub rsp, 48
    mov qword [rsp+32], 0
    mov qword [rsp+40], 0
    call CreateDIBSection
    add rsp, 48
    test rax, rax
    jz .resize_fail
    mov [rbp-32], rax

    mov rbx, [rdi + EngineState.hbitmap]
    mov rcx, [rdi + EngineState.mem_dc]
    mov rdx, [rbp-32]
    call SelectObject

    mov rcx, rbx
    test rcx, rcx
    jz .commit_bitmap
    call DeleteObject

.commit_bitmap:
    mov rax, [rbp-32]
    mov [rdi + EngineState.hbitmap], rax
    mov rax, [rbp-24]
    mov [rdi + EngineState.framebuffer], rax

.update_state_only:
    mov eax, [rbp-4]
    mov [rdi + EngineState.width], eax
    mov [rdi + EngineState.bmi_width], eax
    mov ecx, eax
    imul ecx, AE_PIXEL_SIZE
    mov [rdi + EngineState.stride], ecx

    mov eax, [rbp-8]
    mov [rdi + EngineState.height], eax
    mov ecx, eax
    neg ecx
    mov [rdi + EngineState.bmi_height], ecx

    mov dword [rdi + EngineState.bmi_size], 40
    mov word [rdi + EngineState.bmi_planes], 1
    mov word [rdi + EngineState.bmi_bitcount], 32
    mov dword [rdi + EngineState.bmi_compress], BI_RGB
    mov eax, [rbp-4]
    imul eax, [rbp-8]
    imul eax, AE_PIXEL_SIZE
    mov [rdi + EngineState.bmi_sizeimage], eax
    mov dword [rdi + EngineState.bmi_xppm], 0
    mov dword [rdi + EngineState.bmi_yppm], 0
    mov dword [rdi + EngineState.bmi_clrused], 0
    mov dword [rdi + EngineState.bmi_clrimp], 0

    mov rcx, [rdi + EngineState.hwnd]
    test rcx, rcx
    jz .resize_success
    xor rdx, rdx
    xor r8d, r8d
    call InvalidateRect

.resize_success:
    mov eax, 1
    jmp .resize_done

.resize_fail:
    xor eax, eax

.resize_done:
    add rsp, 128
    pop rbx
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_capture_restore_rect()
; Captures the current window bounds for a future borderless restore.
; ============================================================================
ae_capture_restore_rect:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 64

    lea rdi, [rel g_engine]
    mov rcx, [rdi + EngineState.hwnd]
    test rcx, rcx
    jz .done

    lea rdx, [rbp-32]
    call GetWindowRect
    test eax, eax
    jz .done

    mov eax, [rbp-32]
    mov [rdi + EngineState.restore_left], eax
    mov eax, [rbp-28]
    mov [rdi + EngineState.restore_top], eax
    mov eax, [rbp-24]
    mov [rdi + EngineState.restore_right], eax
    mov eax, [rbp-20]
    mov [rdi + EngineState.restore_bottom], eax
    mov dword [rdi + EngineState.restore_valid], 1

.done:
    add rsp, 64
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_apply_windowed_style()
; Restores the standard overlapped window style.
; ============================================================================
ae_apply_windowed_style:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 32

    lea rdi, [rel g_engine]
    mov rcx, [rdi + EngineState.hwnd]
    test rcx, rcx
    jz .done

    mov edx, GWL_STYLE
    mov r8d, WS_OVERLAPPEDWINDOW | WS_VISIBLE
    call SetWindowLongPtrA

    mov rcx, [rdi + EngineState.hwnd]
    mov edx, GWL_EXSTYLE
    mov r8d, WS_EX_APPWINDOW
    call SetWindowLongPtrA

.done:
    add rsp, 32
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_apply_borderless_style()
; Applies a borderless visible window style for fullscreen presentation.
; ============================================================================
ae_apply_borderless_style:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 32

    lea rdi, [rel g_engine]
    mov rcx, [rdi + EngineState.hwnd]
    test rcx, rcx
    jz .done

    mov edx, GWL_STYLE
    mov r8d, WS_POPUP | WS_VISIBLE
    call SetWindowLongPtrA

    mov rcx, [rdi + EngineState.hwnd]
    mov edx, GWL_EXSTYLE
    mov r8d, WS_EX_APPWINDOW
    call SetWindowLongPtrA

.done:
    add rsp, 32
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_restore_windowed_bounds() -> eax (1=ok, 0=fail)
; Restores the saved window rect after leaving borderless fullscreen.
; ============================================================================
ae_restore_windowed_bounds:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 96

    lea rdi, [rel g_engine]
    cmp dword [rdi + EngineState.restore_valid], 0
    je .fallback_size

    mov eax, [rdi + EngineState.restore_right]
    sub eax, [rdi + EngineState.restore_left]
    mov [rbp-4], eax
    mov eax, [rdi + EngineState.restore_bottom]
    sub eax, [rdi + EngineState.restore_top]
    mov [rbp-8], eax

    mov rcx, [rdi + EngineState.hwnd]
    xor rdx, rdx
    mov r8d, [rdi + EngineState.restore_left]
    mov r9d, [rdi + EngineState.restore_top]
    sub rsp, 64
    mov eax, [rbp-4]
    mov [rsp+32], eax
    mov eax, [rbp-8]
    mov [rsp+40], eax
    mov dword [rsp+48], SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED
    call SetWindowPos
    add rsp, 64
    test eax, eax
    jz .fail
    mov eax, 1
    jmp .done

.fallback_size:
    mov dword [rbp-48], 0
    mov dword [rbp-44], 0
    mov eax, [rdi + EngineState.width]
    mov [rbp-40], eax
    mov eax, [rdi + EngineState.height]
    mov [rbp-36], eax

    lea rcx, [rbp-48]
    mov edx, WS_OVERLAPPEDWINDOW
    xor r8d, r8d
    xor r9d, r9d
    call AdjustWindowRectEx

    mov eax, [rbp-40]
    sub eax, [rbp-48]
    mov [rbp-4], eax
    mov eax, [rbp-36]
    sub eax, [rbp-44]
    mov [rbp-8], eax

    mov rcx, [rdi + EngineState.hwnd]
    xor rdx, rdx
    xor r8d, r8d
    xor r9d, r9d
    sub rsp, 64
    mov eax, [rbp-4]
    mov [rsp+32], eax
    mov eax, [rbp-8]
    mov [rsp+40], eax
    mov dword [rsp+48], SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED
    call SetWindowPos
    add rsp, 64
    test eax, eax
    jz .fail
    mov eax, 1
    jmp .done

.fail:
    xor eax, eax

.done:
    add rsp, 96
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_set_window_mode(mode: ecx) -> eax (1=ok, 0=fail)
; Switches between windowed, maximized, and borderless fullscreen modes.
; ============================================================================
ae_set_window_mode:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 64

    mov [rbp-4], ecx
    cmp ecx, AE_WINDOWMODE_WINDOWED
    jl .fail
    cmp ecx, AE_WINDOWMODE_BORDERLESS_FULLSCREEN
    jg .fail

    lea rdi, [rel g_engine]
    mov rcx, [rdi + EngineState.hwnd]
    test rcx, rcx
    jz .fail

    mov eax, [rdi + EngineState.window_mode]
    cmp eax, [rbp-4]
    je .success

    mov eax, [rbp-4]
    cmp eax, AE_WINDOWMODE_WINDOWED
    je .to_windowed
    cmp eax, AE_WINDOWMODE_MAXIMIZED
    je .to_maximized

    mov eax, [rdi + EngineState.window_mode]
    cmp eax, AE_WINDOWMODE_MAXIMIZED
    jne .capture_borderless_rect
    mov rcx, [rdi + EngineState.hwnd]
    mov edx, SW_RESTORE
    call ShowWindow

.capture_borderless_rect:
    call ae_capture_restore_rect
    call ae_apply_borderless_style

    lea rdi, [rel g_engine]
    mov dword [rdi + EngineState.window_mode], AE_WINDOWMODE_BORDERLESS_FULLSCREEN

    mov ecx, SM_CXSCREEN
    call GetSystemMetrics
    mov [rbp-8], eax
    mov ecx, SM_CYSCREEN
    call GetSystemMetrics
    mov [rbp-12], eax

    mov rcx, [rdi + EngineState.hwnd]
    xor rdx, rdx
    xor r8d, r8d
    xor r9d, r9d
    sub rsp, 64
    mov eax, [rbp-8]
    mov [rsp+32], eax
    mov eax, [rbp-12]
    mov [rsp+40], eax
    mov dword [rsp+48], SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED
    call SetWindowPos
    add rsp, 64
    test eax, eax
    jz .fail
    mov eax, 1
    jmp .done

.to_windowed:
    mov eax, [rdi + EngineState.window_mode]
    cmp eax, AE_WINDOWMODE_BORDERLESS_FULLSCREEN
    jne .show_windowed
    call ae_apply_windowed_style
    lea rdi, [rel g_engine]
    mov dword [rdi + EngineState.window_mode], AE_WINDOWMODE_WINDOWED
    call ae_restore_windowed_bounds
    test eax, eax
    jz .fail

.show_windowed:
    lea rdi, [rel g_engine]
    mov dword [rdi + EngineState.window_mode], AE_WINDOWMODE_WINDOWED
    mov rcx, [rdi + EngineState.hwnd]
    mov edx, SW_RESTORE
    call ShowWindow
    mov eax, 1
    jmp .done

.to_maximized:
    mov eax, [rdi + EngineState.window_mode]
    cmp eax, AE_WINDOWMODE_BORDERLESS_FULLSCREEN
    jne .show_maximized
    call ae_apply_windowed_style
    lea rdi, [rel g_engine]
    mov dword [rdi + EngineState.window_mode], AE_WINDOWMODE_WINDOWED
    call ae_restore_windowed_bounds
    test eax, eax
    jz .fail

.show_maximized:
    lea rdi, [rel g_engine]
    mov rcx, [rdi + EngineState.hwnd]
    mov edx, SW_SHOWMAXIMIZED
    call ShowWindow
    mov dword [rdi + EngineState.window_mode], AE_WINDOWMODE_MAXIMIZED
    mov eax, 1
    jmp .done

.success:
    mov eax, 1
    jmp .done

.fail:
    xor eax, eax

.done:
    add rsp, 64
    pop rdi
    pop rbp
    ret

; ============================================================================
; ae_resize_window(width: ecx, height: edx) -> eax (1=ok, 0=fail)
; Resizes the window client area. WM_SIZE performs the backbuffer swap.
; ============================================================================
ae_resize_window:
    push rbp
    mov rbp, rsp
    push rdi
    sub rsp, 96

    cmp ecx, 1
    jl .resize_fail
    cmp edx, 1
    jl .resize_fail

    mov [rbp-4], ecx                    ; new width
    mov [rbp-8], edx                    ; new height

    lea rdi, [rel g_engine]
    mov rax, [rdi + EngineState.hwnd]
    test rax, rax
    jz .resize_fail

    cmp dword [rdi + EngineState.window_mode], AE_WINDOWMODE_WINDOWED
    je .resize_window_frame

    mov ecx, AE_WINDOWMODE_WINDOWED
    call ae_set_window_mode
    test eax, eax
    jz .resize_fail

    lea rdi, [rel g_engine]

.resize_window_frame:

    mov dword [rbp-48], 0
    mov dword [rbp-44], 0
    mov eax, [rbp-4]
    mov [rbp-40], eax
    mov eax, [rbp-8]
    mov [rbp-36], eax

    lea rcx, [rbp-48]
    mov edx, WS_OVERLAPPEDWINDOW
    xor r8d, r8d
    xor r9d, r9d
    call AdjustWindowRectEx

    mov eax, [rbp-40]
    sub eax, [rbp-48]
    mov [rbp-12], eax                   ; outer width
    mov eax, [rbp-36]
    sub eax, [rbp-44]
    mov [rbp-16], eax                   ; outer height

    mov rcx, [rdi + EngineState.hwnd]
    xor rdx, rdx
    xor r8d, r8d
    xor r9d, r9d
    sub rsp, 64
    mov eax, [rbp-12]
    mov [rsp+32], eax
    mov eax, [rbp-16]
    mov [rsp+40], eax
    mov dword [rsp+48], SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE
    call SetWindowPos
    add rsp, 64
    test eax, eax
    jz .resize_fail

    mov eax, 1
    jmp .resize_done

.resize_fail:
    xor eax, eax

.resize_done:
    add rsp, 96
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
    cmp edx, WM_SIZE
    je .on_size
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

.on_size:
    lea rdi, [rel g_engine]
    mov eax, [rbp-24]
    cmp eax, SIZE_MINIMIZED
    je .size_done

    mov rax, [rbp-32]
    movzx ecx, ax
    shr rax, 16
    movzx edx, ax
    cmp ecx, 1
    jl .size_done
    cmp edx, 1
    jl .size_done

    cmp dword [rdi + EngineState.window_mode], AE_WINDOWMODE_BORDERLESS_FULLSCREEN
    je .apply_size

    mov eax, [rbp-24]
    cmp eax, SIZE_MAXIMIZED
    jne .mark_windowed
    mov dword [rdi + EngineState.window_mode], AE_WINDOWMODE_MAXIMIZED
    jmp .apply_size

.mark_windowed:
    mov dword [rdi + EngineState.window_mode], AE_WINDOWMODE_WINDOWED

.apply_size:
    call ae_apply_client_resize

.size_done:
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
