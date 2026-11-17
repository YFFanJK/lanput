# LANPUT 0.7.2 · 最大化与任务栏

无边框窗口通过 WM_GETMINMAXINFO 使用当前显示器的物理像素工作区确定最大化位置和尺寸，避免播放控制栏与状态栏延伸到 Windows 任务栏后面。影院全屏使用当前显示器的完整区域；退出全屏后重新遵循工作区边界。

边界取自 MonitorFromWindow/GetMonitorInfo，不写死主显示器高度或任务栏高度。最小跟踪尺寸按窗口 DPI 保留。工作区、显示配置和 DPI 变化后会重新检查已最大化窗口的位置。关闭窗口时移除原生消息挂钩。

当前实机验证：屏幕 2880×1920，工作区 2880×1824。记忆的最大化启动、从普通窗口点击最大化、从影院全屏返回最大化，窗口均为 0,0,2880,1824。播放按钮、播放信息、音量、时间标签和底部状态文字均落在工作区内。影院全屏覆盖完整显示器，正常还原与退出通过。未实测额外显示器和全部任务栏停靠配置。

依据：[WM_GETMINMAXINFO](https://learn.microsoft.com/windows/win32/winmsg/wm-getminmaxinfo)、[多显示器窗口定位](https://learn.microsoft.com/windows/win32/gdi/positioning-objects-on-multiple-display-monitors)。
