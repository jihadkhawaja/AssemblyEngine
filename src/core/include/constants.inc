; ============================================================================
; AssemblyEngine - Constants and Macros
; Target: x86-64 Windows (NASM)
; ============================================================================

; --- Engine Limits ---
%define AE_MAX_SPRITES      1024
%define AE_MAX_SOUNDS       256
%define AE_MAX_ENTITIES      4096
%define AE_SPRITE_ENTRY_SIZE 32       ; bytes per sprite descriptor

; --- Pixel Format (BGRA) ---
%define AE_PIXEL_SIZE       4
%define AE_BLUE_OFFSET      0
%define AE_GREEN_OFFSET     1
%define AE_RED_OFFSET       2
%define AE_ALPHA_OFFSET     3

; --- Window Style ---
%define WS_OVERLAPPEDWINDOW 0x00CF0000
%define WS_VISIBLE          0x10000000
%define WS_EX_APPWINDOW     0x00040000

; --- Window Messages ---
%define WM_DESTROY          0x0002
%define WM_SIZE             0x0005
%define WM_PAINT            0x000F
%define WM_CLOSE            0x0010
%define WM_QUIT             0x0012
%define WM_KEYDOWN          0x0100
%define WM_KEYUP            0x0101
%define WM_MOUSEMOVE        0x0200
%define WM_LBUTTONDOWN      0x0201
%define WM_LBUTTONUP        0x0202
%define WM_RBUTTONDOWN      0x0204
%define WM_RBUTTONUP        0x0205
%define WM_MBUTTONDOWN      0x0207
%define WM_MBUTTONUP        0x0208

; --- Virtual Key Codes ---
%define VK_ESCAPE           0x1B
%define VK_SPACE            0x20
%define VK_LEFT             0x25
%define VK_UP               0x26
%define VK_RIGHT            0x27
%define VK_DOWN             0x28

; --- Raster Operations ---
%define SRCCOPY             0x00CC0020
%define DIB_RGB_COLORS      0

; --- ShowWindow Commands ---
%define SW_SHOW             5
%define SW_SHOWDEFAULT      10

; --- PeekMessage Flags ---
%define PM_REMOVE           0x0001

; --- Cursor ---
%define IDC_ARROW           32512

; --- Memory ---
%define PAGE_READWRITE      0x04
%define MEM_COMMIT          0x1000
%define MEM_RESERVE         0x2000
%define MEM_RELEASE         0x8000

; --- BITMAPINFOHEADER ---
%define BI_RGB              0

; --- Audio ---
%define SND_MEMORY          0x0004
%define SND_ASYNC           0x0001
%define SND_NODEFAULT       0x0002

; --- WNDCLASSEX size ---
%define WNDCLASSEX_SIZE     80

; --- MSG structure offsets (64-bit) ---
%define MSG_SIZE            48

; --- Alignment macros ---
%macro ALIGN_STACK 0
    and rsp, -16
%endmacro

; --- Win64 shadow space ---
%macro SHADOW_SPACE 0
    sub rsp, 32
%endmacro

%macro RESTORE_SHADOW 0
    add rsp, 32
%endmacro

; --- Function prologue/epilogue with frame pointer ---
%macro FUNC_PROLOG 0-1 0
    push rbp
    mov rbp, rsp
    sub rsp, %1 + 32          ; local vars + shadow space
%endmacro

%macro FUNC_EPILOG 0
    mov rsp, rbp
    pop rbp
    ret
%endmacro

; --- Save/restore non-volatile registers ---
%macro SAVE_NONVOL 0
    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15
%endmacro

%macro RESTORE_NONVOL 0
    pop r15
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbx
%endmacro
