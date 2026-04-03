; ============================================================================
; AssemblyEngine - Windows API Declarations
; Target: x86-64 Windows (NASM)
; ============================================================================

; --- kernel32.dll ---
extern GetModuleHandleA
extern VirtualAlloc
extern VirtualFree
extern QueryPerformanceFrequency
extern QueryPerformanceCounter
extern GetLastError
extern ExitProcess
extern LoadLibraryA
extern GetProcAddress

; --- user32.dll ---
extern RegisterClassExA
extern CreateWindowExA
extern ShowWindow
extern UpdateWindow
extern DestroyWindow
extern DefWindowProcA
extern PeekMessageA
extern TranslateMessage
extern DispatchMessageA
extern PostQuitMessage
extern GetDC
extern ReleaseDC
extern LoadCursorA
extern SetWindowTextA
extern GetClientRect
extern AdjustWindowRectEx
extern InvalidateRect

; --- gdi32.dll ---
extern CreateCompatibleDC
extern CreateDIBSection
extern SelectObject
extern DeleteObject
extern DeleteDC
extern BitBlt
extern StretchDIBits
extern SetDIBitsToDevice

; --- winmm.dll ---
extern PlaySoundA
extern waveOutOpen
extern waveOutWrite
extern waveOutClose
extern waveOutPrepareHeader
extern waveOutUnprepareHeader
extern waveOutReset
