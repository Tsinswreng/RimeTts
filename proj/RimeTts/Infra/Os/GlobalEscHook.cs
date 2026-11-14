namespace RimeTts.Infra.Os;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

[Doc($$"""
Win32 全域 Esc 鉤子：按 Esc 立刻打斷當前朗讀。

*為何要全域而不是視窗內*：本工具沒有視窗、沒有焦點；使用者在打字，焦點在別人的程式裡。
只有 `WH_KEYBOARD_LL`（低級鍵盤鉤子）能在任何焦點下看到按鍵。

*為何要一條自己的線程*：低級鉤子必須由「裝它的那條線程」跑消息循環才能收到回呼。
把它放在線程池的線程上會被某次阻塞拖住；故開一條背景線程專職收消息。

*為何不吞掉按鍵*：`HookCallback` 一律回 `CallNextHookEx`，即只旁聽、不改行為——
使用者按 Esc 該做的事（關對話框、取消輸入）一樣會發生。

*為何在 Dispose 裡 PostThreadMessage(WM_QUIT)*：`GetMessage` 是阻塞的，
沒有消息進來它就永遠不返回；用 `WM_QUIT` 讓消息循環自己結束，再 Join 線程。
這樣「卸鉤子」與「線程退出」的順序才是確定的。
""")]
public sealed class GlobalEscHook : IGlobalCancelHotkey{

	[Doc("低級鍵盤鉤子的類型編號（WinUser.h 的 WH_KEYBOARD_LL）。")]
	const i32 WH_KEYBOARD_LL = 13;

	[Doc("按鍵按下消息。")]
	const i32 WM_KEYDOWN = 0x0100;

	[Doc("系統鍵按下消息（例如 Alt 組合）。")]
	const i32 WM_SYSKEYDOWN = 0x0104;

	[Doc("退出消息循環用的自訂消息號。")]
	const u32 WM_QUIT = 0x0012;

	[Doc("Esc 的虛擬鍵碼。")]
	const i32 VK_ESCAPE = 0x1B;

	[Doc("熱鍵按下的通知。")]
	public event Action? Pressed;

	[Doc("鉤子線程是否已經收到過 WM_QUIT 的準備信號（建構鉤子完成）。")]
	readonly ManualResetEventSlim Ready = new(false);

	[Doc("委託必須存成欄位：交給 Win32 之後若被 GC 回收，回呼會踩到已釋放的委託。")]
	readonly LowLevelKeyboardProc Proc;

	[Doc("鉤子句柄；Zero 表示沒裝上。")]
	IntPtr Hook = IntPtr.Zero;

	[Doc("鉤子線程的線程 id，Dispose 時用來投遞 WM_QUIT。")]
	u32 ThreadId;

	[Doc("專職跑消息循環的背景線程。")]
	Thread? Worker;

	[Doc("是否已經裝過；重複 Start 是 no-op。")]
	bool Started;

	[Doc("把回呼委託先建好，確保整個生命週期只有一份。")]
	public GlobalEscHook(){
		Proc = HookCallback;
	}

	[Doc($$"""
	裝鉤子並進入收聽。
	#Throw[{{nameof(TypedErr)}}][`SetWindowsHookEx` 失敗（{{nameof(KeysErr.Hotkey.EscHookFailed__)}}）時。]
	""")]
	public void Start(){
		if(Started){
			return;
		}
		Worker = new Thread(HookThreadMain){
			IsBackground = true,
			Name = "RimeTts.GlobalEscHook",
		};
		Worker.Start();
		// 等鉤子裝好（或失敗）再返回，否則呼叫方無法得知 Start 是否真的生效。
		Ready.Wait();
		if(Hook == IntPtr.Zero){
			throw KeysErr.Hotkey.EscHookFailed__.ToErr(
				new Win32Exception(Marshal.GetLastWin32Error()).Message);
		}
		Started = true;
	}

	[Doc("停鉤子、結束鉤子線程。重複呼叫安全。")]
	public void Dispose(){
		if(ThreadId != 0){
			PostThreadMessage(ThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
		}
		Worker?.Join(TimeSpan.FromSeconds(2));
		if(Hook != IntPtr.Zero){
			UnhookWindowsHookEx(Hook);
			Hook = IntPtr.Zero;
		}
		Started = false;
		Ready.Dispose();
	}

	[Doc("Win32 回呼：只旁聽 Esc 的按下，其他一律交給下一個鉤子。")]
	IntPtr HookCallback(i32 nCode, IntPtr wParam, IntPtr lParam){
		if(nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)){
			var Kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
			if(Kb.vkCode == VK_ESCAPE){
				Pressed?.Invoke();
			}
		}
		return CallNextHookEx(Hook, nCode, wParam, lParam);
	}

	[Doc("鉤子線程主體：裝鉤子 → 跑消息循環 → 卸鉤子。")]
	void HookThreadMain(){
		ThreadId = GetCurrentThreadId();
		using var CurProcess = Process.GetCurrentProcess();
		using var CurModule = CurProcess.MainModule;
		var ModuleName = CurModule?.ModuleName ?? "";
		Hook = SetWindowsHookEx(WH_KEYBOARD_LL, Proc, GetModuleHandle(ModuleName), 0);
		// 先放行 Start，讓它去檢查 Hook 是否為 Zero 並決定要不要拋。
		Ready.Set();
		if(Hook == IntPtr.Zero){
			return;
		}

		try{
			// GetMessage 回 0 表示收到 WM_QUIT，回 -1 表示錯誤；兩者都該結束。
			while(GetMessage(out var Msg, IntPtr.Zero, 0, 0) > 0){
				TranslateMessage(ref Msg);
				DispatchMessage(ref Msg);
			}
		}
		finally{
			if(Hook != IntPtr.Zero){
				UnhookWindowsHookEx(Hook);
				Hook = IntPtr.Zero;
			}
		}
	}

	[Doc("低級鍵盤回呼的委託形狀。")]
	delegate IntPtr LowLevelKeyboardProc(i32 nCode, IntPtr wParam, IntPtr lParam);

	[Doc("Win32 的 MSG；只為了跑消息循環。")]
	[StructLayout(LayoutKind.Sequential)]
	struct MSG{
		public IntPtr hwnd;
		public u32 message;
		public IntPtr wParam;
		public IntPtr lParam;
		public u32 time;
		public POINT pt;
		public u32 lPrivate;
	}

	[Doc("Win32 的 POINT。")]
	[StructLayout(LayoutKind.Sequential)]
	struct POINT{
		public i32 x;
		public i32 y;
	}

	[Doc("低級鍵盤鉤子的回呼參數。")]
	[StructLayout(LayoutKind.Sequential)]
	struct KBDLLHOOKSTRUCT{
		public u32 vkCode;
		public u32 scanCode;
		public u32 flags;
		public u32 time;
		public IntPtr dwExtraInfo;
	}

	[DllImport("user32.dll", SetLastError = true)]
	static extern IntPtr SetWindowsHookEx(i32 idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, u32 dwThreadId);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool UnhookWindowsHookEx(IntPtr hhk);

	[DllImport("user32.dll")]
	static extern IntPtr CallNextHookEx(IntPtr hhk, i32 nCode, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	static extern IntPtr GetModuleHandle(str lpModuleName);

	[DllImport("user32.dll", SetLastError = true)]
	static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, u32 wMsgFilterMin, u32 wMsgFilterMax);

	[DllImport("user32.dll")]
	static extern bool TranslateMessage([In] ref MSG lpMsg);

	[DllImport("user32.dll")]
	static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

	[DllImport("user32.dll", SetLastError = true)]
	static extern bool PostThreadMessage(u32 idThread, u32 Msg, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll")]
	static extern u32 GetCurrentThreadId();
}
