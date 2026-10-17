// WinRT/CsWinRT 内部类型缺口占位（**不是官方实现**）
// ComInterfaceEntry 由 CsWinRT 投影在编译期生成，骨架里仅作为返回类型出现。
#nullable disable
using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct ComInterfaceEntry
{
    public IntPtr IID;
    public IntPtr Vtable;
}
