<#
Unity 에디터 창만 정확히 캡처해서 지정한 경로에 PNG로 저장한다.
전체 화면 캡처와 달리 다른 창(탐색기, 브라우저 등)이 섞여 찍히지 않는다.
추가 설치 없이 Windows 기본 System.Drawing + user32.dll(PrintWindow)만 사용한다.

사용법:
  powershell -File tools/capture-unity-window.ps1 -OutPath "E:\screenshot\my-shot.png"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$OutPath
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

public class UnityWindowCapture {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    public struct RECT { public int Left, Top, Right, Bottom; }

    public static void Capture(IntPtr hWnd, string path) {
        RECT rect;
        GetWindowRect(hWnd, out rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        Bitmap bmp = new Bitmap(width, height);
        Graphics gfx = Graphics.FromImage(bmp);
        IntPtr hdc = gfx.GetHdc();
        PrintWindow(hWnd, hdc, 2); // PW_RENDERFULLCONTENT
        gfx.ReleaseHdc(hdc);
        bmp.Save(path, ImageFormat.Png);
        gfx.Dispose();
        bmp.Dispose();
    }
}
"@ -ReferencedAssemblies System.Drawing

$proc = Get-Process -Name "Unity" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) {
    Write-Error "실행 중인 Unity 에디터 창을 찾을 수 없습니다."
    exit 1
}

$outDir = Split-Path -Parent $OutPath
if ($outDir -and -not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

[UnityWindowCapture]::Capture($proc.MainWindowHandle, $OutPath)
Write-Output "Saved: $OutPath ($($proc.MainWindowTitle))"
