; ============================================================================
; AssemblyEngine - Engine State Structure
; Target: x86-64 Windows (NASM)
; Shared state between all assembly modules
; ============================================================================

%include "include/constants.inc"

; --- Engine State Structure ---
; All modules access this global state via [rel g_engine + offset]
struc EngineState
    .hwnd           resq 1          ; HWND - window handle
    .hinstance      resq 1          ; HINSTANCE
    .hdc            resq 1          ; HDC - device context
    .mem_dc         resq 1          ; HDC - memory DC (double buffer)
    .hbitmap        resq 1          ; HBITMAP - DIB section
    .framebuffer    resq 1          ; Pointer to pixel data (BGRA)
    .width          resd 1          ; Framebuffer width
    .height         resd 1          ; Framebuffer height
    .stride         resd 1          ; Bytes per row (width * 4)
    .running        resd 1          ; 1 = engine running, 0 = quit
    .vsync_enabled  resd 1          ; 1 = sync present with compositor, 0 = no sync
    .window_mode    resd 1          ; 0 = windowed, 1 = maximized, 2 = borderless fullscreen
    .restore_valid  resd 1          ; 1 = restore rect captured for borderless fullscreen
    .restore_left   resd 1          ; Stored window left for borderless restore
    .restore_top    resd 1          ; Stored window top for borderless restore
    .restore_right  resd 1          ; Stored window right for borderless restore
    .restore_bottom resd 1          ; Stored window bottom for borderless restore

    ; --- Input State ---
    .keys           resb 256        ; Current frame key states
    .prev_keys      resb 256        ; Previous frame key states
    .mouse_x        resd 1          ; Mouse X position
    .mouse_y        resd 1          ; Mouse Y position
    .mouse_buttons  resd 1          ; Bit 0=Left, 1=Right, 2=Middle
    .prev_mouse_btn resd 1          ; Previous frame mouse buttons

    ; --- Timing ---
    .perf_freq      resq 1          ; QueryPerformanceFrequency
    .last_tick      resq 1          ; Last frame tick count
    .current_tick   resq 1          ; Current tick count
    .delta_time     resd 1          ; Delta time in seconds (float)
    .fps            resd 1          ; Frames per second
    .frame_count    resd 1          ; Frame counter (for FPS calc)
    .fps_accum      resd 1          ; FPS time accumulator (float)

    ; --- Sprites ---
    .sprite_count   resd 1          ; Number of loaded sprites
    .sprite_pad     resd 1          ; Padding for alignment
    .sprites        resq 1          ; Pointer to sprite descriptor array

    ; --- Audio ---
    .sound_count    resd 1          ; Number of loaded sounds
    .sound_pad      resd 1
    .sounds         resq 1          ; Pointer to sound buffer array

    ; --- Memory ---
    .arena_base     resq 1          ; Arena allocator base pointer
    .arena_offset   resq 1          ; Current allocation offset
    .arena_size     resq 1          ; Total arena size

    ; --- BITMAPINFO for DIB ---
    .bmi_size       resd 1          ; biSize = 40
    .bmi_width      resd 1          ; biWidth
    .bmi_height     resd 1          ; biHeight (negative = top-down)
    .bmi_planes     resw 1          ; biPlanes = 1
    .bmi_bitcount   resw 1          ; biBitCount = 32
    .bmi_compress   resd 1          ; biCompression = BI_RGB
    .bmi_sizeimage  resd 1
    .bmi_xppm       resd 1
    .bmi_yppm       resd 1
    .bmi_clrused    resd 1
    .bmi_clrimp     resd 1
endstruc

; --- Sprite Descriptor ---
struc SpriteDesc
    .pixels         resq 1          ; Pointer to pixel data (BGRA)
    .width          resd 1          ; Sprite width
    .height         resd 1          ; Sprite height
    .pitch          resd 1          ; Bytes per row
    .flags          resd 1          ; Bit 0 = has alpha
endstruc

; --- Sound Descriptor ---
struc SoundDesc
    .data           resq 1          ; Pointer to WAV data
    .size           resd 1          ; Data size in bytes
    .flags          resd 1          ; Playback flags
endstruc
