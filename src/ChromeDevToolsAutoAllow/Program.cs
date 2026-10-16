// C# 5 / .NET Framework 4.x. No NuGet, service, polling loop or injected DLL.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace ChromeDevToolsAutoAllow
{
    internal static class Icons
    {
        internal const string AppResource = "ChromeDevToolsAutoAllow.AppIcon";
        internal const string TrayResource = "ChromeDevToolsAutoAllow.TrayIcon";
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateIconFromResourceEx(byte[] bits, uint size, bool icon, uint version, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        internal static Stream Open(string name)
        {
            var stream = typeof(Icons).Assembly.GetManifestResourceStream(name);
            if (stream == null) throw new InvalidOperationException("Missing embedded icon: " + name);
            return stream;
        }
        internal static Icon Load(string name, Size size)
        {
            // The .NET Framework ICO selector can skip a 256px (zero-byte dimensions) PNG frame.
            // Explicitly load that native resource instead of silently returning the 128px image.
            if (size.Width == 256 && size.Height == 256)
            {
                using (var stream = Open(name))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadUInt16(); reader.ReadUInt16(); int count = reader.ReadUInt16();
                    for (int i = 0; i < count; i++)
                    {
                        int width = reader.ReadByte(), height = reader.ReadByte();
                        reader.ReadByte(); reader.ReadByte(); reader.ReadUInt16(); reader.ReadUInt16();
                        int length = reader.ReadInt32(), offset = reader.ReadInt32();
                        if (width != 0 || height != 0) continue;
                        if (length <= 0 || offset < 6 + 16 * count || (long)offset + length > stream.Length) throw new InvalidDataException("Invalid large icon frame.");
                        stream.Position = offset; byte[] frame = reader.ReadBytes(length);
                        IntPtr handle = CreateIconFromResourceEx(frame, (uint)frame.Length, true, 0x00030000, 256, 256, 0);
                        if (handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                        try { using (var original = Icon.FromHandle(handle)) return (Icon)original.Clone(); }
                        finally { DestroyIcon(handle); }
                    }
                    throw new InvalidDataException("No 256px frame in " + name);
                }
            }
            using (var stream = Open(name))
            using (var original = new Icon(stream, size)) return (Icon)original.Clone();
        }
        internal static Icon Tray() { return Load(TrayResource, SystemInformation.SmallIconSize); }
        internal static void Diagnostics(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (int size in new int[] { 16, 32, 48, 256 })
            using (var icon = Load(AppResource, new Size(size, size)))
            using (var bitmap = icon.ToBitmap()) bitmap.Save(System.IO.Path.Combine(folder, "embedded-app-" + size + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            foreach (int size in new int[] { 16, 20, 24, 32, 48 })
            using (var icon = Load(TrayResource, new Size(size, size)))
            using (var bitmap = icon.ToBitmap()) bitmap.Save(System.IO.Path.Combine(folder, "embedded-tray-" + size + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            using (var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
            using (var bitmap = icon.ToBitmap()) bitmap.Save(System.IO.Path.Combine(folder, "extracted-exe-icon.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    internal static class Native
    {
        internal delegate void WinEvent(IntPtr hook, uint evt, IntPtr hwnd, int obj, int child, uint tid, uint time);
        internal delegate bool EnumProc(IntPtr hwnd, IntPtr param);
        [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEvent callback, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
        internal static string ClassName(IntPtr hwnd) { var b = new StringBuilder(256); GetClassName(hwnd, b, b.Capacity); return b.ToString(); }
        internal static int Pid(IntPtr hwnd) { uint p; GetWindowThreadProcessId(hwnd, out p); return (int)p; }
    }

    internal sealed class Log : IDisposable
    {
        private readonly object gate = new object();
        private readonly StreamWriter writer;
        private bool closed;
        internal readonly string Path;
        internal Log(string path)
        {
            Path = System.IO.Path.GetFullPath(path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            writer = new StreamWriter(new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));
            writer.AutoFlush = true;
        }
        internal void Write(string type, string text)
        {
            lock (gate)
            {
                if (!closed) writer.WriteLine(DateTimeOffset.Now.ToString("o") + " [" + type + "] " + text.Replace('\r', ' ').Replace('\n', ' '));
            }
        }
        public void Dispose() { lock (gate) { if (!closed) { closed = true; writer.Dispose(); } } }
    }

    internal sealed class ProcessPolicy
    {
        internal readonly string Path;
        private readonly int fixturePid;
        internal ProcessPolicy(string path, int fixtureProcessId)
        {
            Path = System.IO.Path.GetFullPath(path);
            fixturePid = fixtureProcessId;
        }
        internal bool NativeCandidate(IntPtr hwnd)
        {
            if (!Native.IsWindowVisible(hwnd)) return false;
            if (fixturePid != 0) return Native.Pid(hwnd) == fixturePid;
            return Native.ClassName(hwnd).StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal);
        }
        internal bool Verify(IntPtr hwnd, out int pid)
        {
            pid = Native.Pid(hwnd);
            if (pid == 0 || !NativeCandidate(hwnd) || (fixturePid != 0 && pid != fixturePid)) return false;
            try
            {
                using (var p = Process.GetProcessById(pid))
                    return !p.HasExited && String.Equals(System.IO.Path.GetFullPath(p.MainModule.FileName), Path, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; } // Inaccessible, elevated or exited process: fail closed.
        }
        internal static string FindChrome()
        {
            string[] roots = { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) };
            foreach (string root in roots)
            {
                string p = System.IO.Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe");
                if (File.Exists(p)) return p;
            }
            throw new FileNotFoundException("Chrome not found. Supply --chrome-path with its exact executable path.");
        }
    }

    internal sealed class Node
    {
        internal string Name = "";
        internal int Type;
        internal int Pid;
        internal bool Enabled = true, Visible = true;
        internal AutomationElement Element;
        internal readonly List<Node> Children = new List<Node>();
        internal static bool IsDocument(int type) { return type == ControlType.Document.Id; }
    }

    internal sealed class Reader
    {
        private int count;
        private Stopwatch clock;
        private readonly int pid;
        internal Reader(int processId) { pid = processId; }
        internal Node Read(IntPtr hwnd)
        {
            count = 0; clock = Stopwatch.StartNew();
            return ReadOne(AutomationElement.FromHandle(hwnd), 0);
        }
        private Node ReadOne(AutomationElement e, int depth)
        {
            if (e == null) return null;
            if (++count > 400 || depth > 18 || clock.ElapsedMilliseconds > 2000)
                throw new InvalidOperationException("UIA snapshot budget exceeded; no approval attempted.");
            var info = e.Current;
            if (info.ProcessId != pid) return null;
            var n = new Node { Name = info.Name ?? "", Type = info.ControlType.Id, Pid = info.ProcessId, Enabled = info.IsEnabled, Visible = !info.IsOffscreen, Element = e };
            // Never traverse webpage content. ARIA dialogs inside a page are not browser consent.
            if (Node.IsDocument(n.Type) || info.ClassName == "Chrome_RenderWidgetHostHWND") return null;
            if (!n.Visible) return n;
            var walker = TreeWalker.RawViewWalker;
            for (var child = walker.GetFirstChild(e); child != null; child = walker.GetNextSibling(child))
            {
                var item = ReadOne(child, depth + 1);
                if (item != null) n.Children.Add(item);
            }
            return n;
        }
    }

    internal sealed class Match
    {
        internal Node Dialog, Allow;
        internal string Language;
        internal string Key()
        {
            return String.Join(".", Allow.Element.GetRuntimeId().Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray());
        }
    }

    internal static class Identity
    {
        // Chrome exposes the same dialog through both its browser HWND and its popup HWND.
        // HWND must NOT be part of the key: UIA RuntimeId already identifies the actual button.
        internal static string Of(int pid, string runtimeId) { return pid.ToString(CultureInfo.InvariantCulture) + ":" + runtimeId; }
    }

    internal static class Matcher
    {
        internal static bool Title(string s)
        {
            return s == "Allow remote debugging?" || s == "要允许远程调试吗？";
        }
        private static void Flatten(Node n, List<Node> list)
        {
            if (n == null || !n.Visible || Node.IsDocument(n.Type)) return;
            list.Add(n);
            foreach (var c in n.Children) Flatten(c, list);
        }
        internal static List<Match> Find(Node root, int pid)
        {
            var nodes = new List<Node>(); Flatten(root, nodes);
            var result = new List<Match>();
            foreach (var n in nodes)
            {
                if (n.Type == ControlType.Text.Id || n.Type == ControlType.Button.Id) continue;
                // Title must be the container name or a direct text/header child, not any descendant.
                if (!Title(n.Name) && !n.Children.Any(c => c.Visible && Title(c.Name) && c.Type == ControlType.Text.Id)) continue;
                var inside = new List<Node>(); Flatten(n, inside);
                if (inside.Any(x => x.Pid != pid)) continue;
                string text = String.Join("\n", inside.Select(x => x.Name).ToArray());
                bool english = text.IndexOf("external app", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("full control", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("cookies", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("trusted apps", StringComparison.OrdinalIgnoreCase) >= 0;
                bool chinese = text.Contains("外部应用") && text.Contains("完全控制") && text.Contains("Cookie") && text.Contains("受信任");
                if (!english && !chinese) continue;
                string allow = english ? "Allow" : "允许", cancel = english ? "Cancel" : "取消";
                var yes = inside.Where(x => x.Type == ControlType.Button.Id && x.Name == allow).ToList();
                var no = inside.Where(x => x.Type == ControlType.Button.Id && x.Name == cancel).ToList();
                if (yes.Count != 1 || no.Count != 1 || !yes[0].Enabled || !no[0].Enabled) continue;
                if (!result.Any(x => Object.ReferenceEquals(x.Allow, yes[0])))
                    result.Add(new Match { Dialog = n, Allow = yes[0], Language = english ? "en" : "zh-CN" });
            }
            return result;
        }
    }

    internal enum ApprovalMode { Observe, Temporary, Permanent }

    internal sealed class Lease
    {
        private readonly object gate = new object();
        private readonly Stopwatch time = Stopwatch.StartNew();
        private readonly Func<long> now;
        private long until;
        private bool permanent;
        internal Lease() { now = delegate { return time.ElapsedMilliseconds; }; }
        internal Lease(Func<long> clock) { now = clock; }
        internal ApprovalMode Mode { get { lock (gate) return permanent ? ApprovalMode.Permanent : (until > now() ? ApprovalMode.Temporary : ApprovalMode.Observe); } }
        internal bool Active { get { return Mode != ApprovalMode.Observe; } }
        internal void Arm(int minutes) { lock (gate) { permanent = false; until = now() + minutes * 60000L; } }
        internal void ArmPermanent() { lock (gate) { permanent = true; until = 0; } }
        internal void Disarm() { lock (gate) { permanent = false; until = 0; } }
    }

    internal sealed class SettingsStore
    {
        internal readonly string Path;
        private readonly string chromePath;
        internal SettingsStore(string path, string chrome)
        {
            Path = System.IO.Path.GetFullPath(path);
            chromePath = System.IO.Path.GetFullPath(chrome);
        }
        internal bool Load(out string warning)
        {
            warning = null;
            try
            {
                if (!File.Exists(Path)) return false;
                if (new FileInfo(Path).Length > 4096) throw new InvalidDataException("Settings exceed the size limit.");
                string[] lines = File.ReadAllLines(Path, Encoding.UTF8);
                if (lines.Length != 3 || lines[0] != "version=1" ||
                    (lines[1] != "permanentAutoAllow=true" && lines[1] != "permanentAutoAllow=false") ||
                    !lines[2].StartsWith("chromePath=", StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid settings format; automatic approval remains disabled.");
                if (!String.Equals(lines[2].Substring(11), chromePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Saved Chrome executable does not match the selected target; automatic approval remains disabled.");
                return lines[1] == "permanentAutoAllow=true";
            }
            catch (Exception e) { warning = e.GetType().Name + ": " + e.Message; return false; }
        }
        internal void Save(bool enabled)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temp = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string text = "version=1\r\npermanentAutoAllow=" + (enabled ? "true" : "false") + "\r\nchromePath=" + chromePath + "\r\n";
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                    file.Write(bytes, 0, bytes.Length); file.Flush(true);
                }
                // Same-directory replacement. If it cannot be saved, the caller remains disarmed.
                if (File.Exists(Path)) File.Replace(temp, Path, null); else File.Move(temp, Path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal sealed class ModeController
    {
        internal readonly Lease Lease;
        internal readonly SettingsStore Settings;
        internal ModeController(Lease lease, SettingsStore settings) { Lease = lease; Settings = settings; }
        internal string Restore(bool forceObserve)
        {
            Lease.Disarm();
            if (forceObserve) return null; // --observe / --no-tray must never restore automatic clicking.
            string warning;
            if (Settings.Load(out warning)) Lease.ArmPermanent();
            return warning;
        }
        internal void EnablePermanent() { Lease.Disarm(); Settings.Save(true); Lease.ArmPermanent(); }
        internal void EnableTemporary() { Lease.Disarm(); Settings.Save(false); Lease.Arm(10); }
        internal void Stop() { Lease.Disarm(); Settings.Save(false); }
    }

    internal static class TrayMenu
    {
        internal const string PermanentLabel = "启用自动允许（永久）…";
        internal static ContextMenuStrip Create(EventHandler status, EventHandler temporary, EventHandler permanent, EventHandler stop, EventHandler scan, EventHandler exit)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("状态 / Status", null, status);
            menu.Items.Add("启用自动允许 10 分钟…", null, temporary);
            menu.Items.Add(PermanentLabel, null, permanent);
            menu.Items.Add("立即停止自动允许", null, stop);
            menu.Items.Add("立即检查一次", null, scan);
            menu.Items.Add("退出", null, exit);
            return menu;
        }
        internal static void Update(ContextMenuStrip menu, ApprovalMode mode)
        {
            ((ToolStripMenuItem)menu.Items[1]).Checked = mode == ApprovalMode.Temporary;
            ((ToolStripMenuItem)menu.Items[2]).Checked = mode == ApprovalMode.Permanent;
        }
        internal static string ModeText(ApprovalMode mode)
        {
            if (mode == ApprovalMode.Permanent) return "自动允许：永久（已保存，重启后恢复）";
            if (mode == ApprovalMode.Temporary) return "自动允许：10 分钟（不保存）";
            return "仅检测，不自动点击";
        }
        internal static void Preview(string path)
        {
            using (var menu = Create(null, null, null, null, null, null))
            {
                menu.AutoSize = false; menu.Size = menu.PreferredSize;
                menu.CreateControl(); menu.PerformLayout();
                using (var bitmap = new Bitmap(menu.Width, menu.Height))
                {
                    menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, menu.Size));
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
    }

    internal enum DialogObservation { Visible, Gone, Unavailable, Cancelled }

    internal static class DialogVerifier
    {
        // Read-only, bounded observations after ONE existing invocation. Never retries an input.
        // This is a settling window, not a hard timeout on a blocked external UIA provider.
        internal static DialogObservation Observe(Func<DialogObservation> read, Func<int, bool> wait)
        {
            DialogObservation last = DialogObservation.Unavailable;
            foreach (int delay in new int[] { 250, 500, 1000, 1500 })
            {
                if (wait(delay)) return DialogObservation.Cancelled;
                try { last = read(); }
                catch (Exception) { last = DialogObservation.Unavailable; }
                if (last == DialogObservation.Gone) return last;
            }
            return last;
        }
    }

    internal sealed class Watcher : IDisposable
    {
        private readonly ProcessPolicy policy;
        private readonly Lease lease;
        private readonly Log log;
        private readonly object gate = new object();
        private readonly Queue<IntPtr> jobs = new Queue<IntPtr>();
        private readonly HashSet<IntPtr> pending = new HashSet<IntPtr>();
        private readonly Dictionary<string, long> observed = new Dictionary<string, long>();
        private readonly Dictionary<string, long> invoked = new Dictionary<string, long>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private readonly List<IntPtr> hooks = new List<IntPtr>();
        private readonly Native.WinEvent callback;
        private readonly Thread worker;
        private volatile bool stopping;
        internal long Events, Queued, Scans, Detected, Invoked, Errors;
        internal long Attempts, Dismissed, Unconfirmed;
        private string lastResult = "尚未识别授权弹窗；未验证 MCP 连接。";
        private int needsManual;
        internal string LastResult { get { return Volatile.Read(ref lastResult); } }
        internal bool NeedsManual { get { return Volatile.Read(ref needsManual) != 0; } }
        internal Action<string> Notify;
        internal Action<string> SafetyStop;
        internal Action ResultChanged;
        internal Watcher(ProcessPolicy p, Lease l, Log output)
        {
            policy = p; lease = l; log = output;
            callback = OnEvent; // Field roots the delegate until all hooks are unregistered.
            worker = new Thread(Work) { IsBackground = true, Name = "Consent-UIA-MTA" };
            worker.SetApartmentState(ApartmentState.MTA);
        }
        internal void Start()
        {
            try
            {
                // Each exact event is registered separately. No enormous event range and no polling timer.
                foreach (uint evt in new uint[] { 0x0003, 0x0010, 0x8002, 0x800C })
                {
                    IntPtr h = Native.SetWinEventHook(evt, evt, IntPtr.Zero, callback, 0, 0, 0x0002); // OUTOFCONTEXT | SKIPOWNPROCESS
                    if (h == IntPtr.Zero) throw new InvalidOperationException("SetWinEventHook failed for " + evt.ToString("X"));
                    hooks.Add(h);
                }
                worker.Start();
                log.Write("START", "mode=" + lease.Mode + "; hooks=4; Chrome=" + policy.Path + "; no periodic window scan");
                ScanExisting();
            }
            catch { Dispose(); throw; }
        }
        internal void ScanExisting()
        {
            // Only at startup or an explicit tray action. Not periodic.
            Native.EnumWindows(delegate(IntPtr h, IntPtr unused) { if (policy.NativeCandidate(h)) Enqueue(h); return true; }, IntPtr.Zero);
        }
        private void OnEvent(IntPtr hook, uint evt, IntPtr hwnd, int obj, int child, uint tid, uint time)
        {
            try
            {
                Interlocked.Increment(ref Events);
                if (stopping || hwnd == IntPtr.Zero) return;
                // Ignore page text changes. Only top-level window name changes are useful here.
                if (evt == 0x800C && (obj != 0 || child != 0)) return;
                if (evt == 0x8002 && obj != 0 && obj != -4) return;
                IntPtr root = Native.GetAncestor(hwnd, 2);
                if (root == IntPtr.Zero) root = hwnd;
                if (policy.NativeCandidate(root)) Enqueue(root);
            }
            catch { Interlocked.Increment(ref Errors); } // Never unwind into user32.
        }
        private void Enqueue(IntPtr hwnd)
        {
            lock (gate)
            {
                if (stopping || pending.Contains(hwnd) || jobs.Count >= 32) return;
                pending.Add(hwnd); jobs.Enqueue(hwnd); Interlocked.Increment(ref Queued);
            }
            wake.Set();
        }
        private void Work()
        {
            while (!stopping)
            {
                IntPtr hwnd = IntPtr.Zero;
                lock (gate) if (jobs.Count != 0) hwnd = jobs.Dequeue();
                if (hwnd == IntPtr.Zero) { WaitHandle.WaitAny(new WaitHandle[] { wake, stop }); continue; }
                try
                {
                    // Bounded settling attempts after an event, not continuous polling.
                    foreach (int delay in new int[] { 120, 300, 550 })
                    {
                        if (stop.WaitOne(delay)) break;
                        if (Inspect(hwnd)) break;
                    }
                }
                catch (Exception e) { Interlocked.Increment(ref Errors); log.Write("ERROR", e.GetType().Name + ": " + e.Message); }
                finally { lock (gate) pending.Remove(hwnd); }
            }
        }
        private bool Inspect(IntPtr hwnd)
        {
            int pid;
            if (!policy.Verify(hwnd, out pid)) return true;
            Interlocked.Increment(ref Scans);
            var matches = Matcher.Find(new Reader(pid).Read(hwnd), pid);
            if (matches.Count != 1) return false; // Ambiguous or missing => no action.
            var match = matches[0];
            string key = Identity.Of(pid, match.Key());
            long now = clock.ElapsedMilliseconds;
            Prune(observed, now);
            if (!observed.ContainsKey(key))
            {
                observed[key] = now; Interlocked.Increment(ref Detected);
                log.Write("DETECTED", "pid=" + pid + "; hwnd=" + hwnd + "; title=" + match.Dialog.Name + "; language=" + match.Language + "; mode=" + lease.Mode + "; Allow enabled; page content excluded");
                SetResult("已识别授权弹窗；" + TrayMenu.ModeText(lease.Mode) + "。尚未验证授权或 MCP 连接。", false);
                if (Notify != null) Notify("已识别 Chrome 远程调试授权框；" + TrayMenu.ModeText(lease.Mode));
            }
            if (!lease.Active) return true;
            if (invoked.ContainsKey(key))
            {
                SetResult("此弹窗已尝试过，结果未重新确认；不会重复操作。请手动处理授权。", true);
                return true;
            }
            // Keep invoked IDs for this entire run. Never retry an uncertain approval after a TTL.
            // Bound memory and stop safely rather than silently forget old approvals.
            if (invoked.Count >= 256)
            {
                lease.Disarm(); log.Write("DISARMED", "256 distinct invocation attempts reached; restart required to reset the safety limit.");
                if (SafetyStop != null) SafetyStop("本次运行已尝试 256 次自动允许，触发保护性暂停。请检查日志后重启程序，再手动启用。");
                return true;
            }
            // Re-read the same live dialog immediately before Invoke; do not rely on cached coordinates.
            int currentPid;
            if (!policy.Verify(hwnd, out currentPid) || currentPid != pid) return true;
            var fresh = Matcher.Find(new Reader(pid).Read(hwnd), pid);
            if (fresh.Count != 1 || fresh[0].Key() != match.Key() || !lease.Active) return true;
            object pattern;
            if (!fresh[0].Allow.Element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
            { RequireManual("NO_INVOKE_PATTERN", "按钮不支持当前调用方式，未执行授权。", null); return true; }
            // An uncertain invoke is not retried automatically (avoid duplicate approval).
            invoked[key] = now;
            Interlocked.Increment(ref Attempts);
            SetResult("已发出一次按钮调用；正在只读检查弹窗状态，尚未确认授权。", false);
            try { ((InvokePattern)pattern).Invoke(); }
            catch (Exception e)
            {
                Interlocked.Increment(ref Errors);
                RequireManual("INVOKE_ERROR", "按钮调用出现异常，无法确认是否生效。", e.GetType().Name + ": " + e.Message);
                return true;
            }
            Interlocked.Increment(ref Invoked);
            log.Write("INVOKE_RETURNED", "pid=" + pid + "; API returned, NOT authorization success; no second invocation will be sent");
            string verificationError = null;
            AutomationElement originalDialog = fresh[0].Dialog.Element;
            DialogObservation outcome = DialogVerifier.Observe(delegate
            {
                // Inspect the original container, not Matcher.Find: a disabled/renamed button
                // makes the matcher fail, but must NOT be mistaken for a closed dialog.
                if (!Native.IsWindow(hwnd)) return DialogObservation.Gone;
                if (Native.Pid(hwnd) != pid) return DialogObservation.Unavailable;
                try
                {
                    var current = originalDialog.Current;
                    if (current.ProcessId != pid || current.IsOffscreen) return DialogObservation.Unavailable;
                    return DialogObservation.Visible;
                }
                catch (ElementNotAvailableException) { return DialogObservation.Gone; }
                catch (Exception e)
                {
                    verificationError = e.GetType().Name + ": " + e.Message;
                    return DialogObservation.Unavailable;
                }
            }, delegate(int delay) { return stop.WaitOne(delay); });
            if (outcome == DialogObservation.Gone)
            {
                Interlocked.Increment(ref Dismissed);
                SetResult("原弹窗已消失；这不证明授权获准，也不证明 MCP 已连接。", false);
                log.Write("DIALOG_DISMISSED", "Original dialog is no longer available; approval and MCP connection remain UNVERIFIED");
                if (Notify != null) Notify(LastResult);
            }
            else if (outcome == DialogObservation.Cancelled)
                SetResult("程序停止，结果检查已取消；授权结果未知。", false);
            else RequireManual("UNCONFIRMED", outcome == DialogObservation.Visible
                ? "按钮接口返回后，授权弹窗仍然存在；没有确认授权成功。"
                : "无法可靠读取原弹窗的状态；没有确认授权成功。", verificationError);
            return true;
        }
        private void SetResult(string message, bool manual)
        {
            Volatile.Write(ref lastResult, message);
            Volatile.Write(ref needsManual, manual ? 1 : 0);
            if (ResultChanged != null) ResultChanged();
        }
        private void RequireManual(string code, string reason, string detail)
        {
            lease.Disarm(); // Stop immediately on the worker, before posting UI/persistence work.
            Interlocked.Increment(ref Unconfirmed);
            string message = reason + "\n\n当前已停止自动允许，请在 Chrome 中手动确认。不会追加点击或键盘操作。\n同一弹窗不会因切换模式而重试。";
            SetResult(message, true);
            log.Write(code, reason + " Auto-approval stopped; manual action required." + (detail == null ? "" : " " + detail));
            if (SafetyStop != null) SafetyStop(message);
            else if (Notify != null) Notify(message);
        }
        private static void Prune(Dictionary<string, long> map, long now)
        {
            foreach (var key in map.Where(x => now - x.Value > 60000).Select(x => x.Key).ToArray()) map.Remove(key);
        }
        internal string Stats()
        {
            return "events=" + Interlocked.Read(ref Events) + "; queued=" + Interlocked.Read(ref Queued) + "; scans=" + Interlocked.Read(ref Scans) + "; detected=" + Interlocked.Read(ref Detected) + "; attempts=" + Interlocked.Read(ref Attempts) + "; invokeReturned=" + Interlocked.Read(ref Invoked) + "; dialogDismissed=" + Interlocked.Read(ref Dismissed) + "; unconfirmed=" + Interlocked.Read(ref Unconfirmed) + "; errors=" + Interlocked.Read(ref Errors);
        }
        public void Dispose()
        {
            if (stopping) return;
            stopping = true; lease.Disarm();
            foreach (var h in hooks) Native.UnhookWinEvent(h);
            hooks.Clear(); stop.Set(); wake.Set();
            // UIA provider calls cannot be safely aborted in-process. Keep UI responsive on shutdown.
            bool joined = !worker.IsAlive || worker.Join(3000);
            log.Write("STOP", Stats() + "; workerJoined=" + joined);
            GC.KeepAlive(callback);
            // If a provider is hung, background worker/resources live only until process exit.
            if (joined) { stop.Dispose(); wake.Dispose(); }
        }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon tray;
        private readonly Icon trayIcon;
        private readonly Watcher watcher;
        private readonly Lease lease;
        private readonly ModeController modes;
        private readonly Log log;
        private readonly Control marshal = new Control();
        private readonly System.Threading.Timer duration;
        private readonly System.Threading.Timer expiry;
        private readonly ContextMenuStrip menu;
        internal TrayContext(ProcessPolicy p, Log output, bool showTray, int seconds, bool forceObserve)
        {
            log = output; lease = new Lease();
            modes = new ModeController(lease, new SettingsStore(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"), p.Path));
            string settingsWarning = modes.Restore(forceObserve || !showTray);
            if (settingsWarning != null) log.Write("SETTINGS_IGNORED", settingsWarning);
            if (lease.Mode == ApprovalMode.Permanent) log.Write("RESTORED", "User's saved permanent mode restored for the same Chrome executable.");
            marshal.CreateControl();
            expiry = new System.Threading.Timer(delegate { Post(UpdateState); }, null, Timeout.Infinite, Timeout.Infinite);
            watcher = new Watcher(p, lease, log);
            if (showTray)
            {
                menu = TrayMenu.Create(
                    delegate { UpdateState(); MessageBox.Show(TrayMenu.ModeText(lease.Mode) + "\n\n最近结果：\n" + watcher.LastResult + "\n\n" + watcher.Stats() + "\n\n注意：attempts 是调用尝试，不是授权成功数；dialogDismissed 也不是 MCP 连接成功数。\n\n设置：" + modes.Settings.Path + "\n\n日志：" + log.Path, "ChromeDevToolsAutoAllow 1.0.0"); },
                    delegate { Enable(false); }, delegate { Enable(true); }, delegate { StopAutomatic(); },
                    delegate { watcher.ScanExisting(); }, delegate { ExitThread(); });
                menu.Opening += delegate { UpdateState(); };
                trayIcon = Icons.Tray();
                tray = new NotifyIcon { Icon = trayIcon, Text = "ChromeDevToolsAutoAllow · 仅检测", ContextMenuStrip = menu, Visible = true };
                watcher.Notify = delegate(string message)
                {
                    Post(delegate { if (tray.Visible) tray.ShowBalloonTip(4000, "ChromeDevToolsAutoAllow", message, ToolTipIcon.Info); });
                };
                watcher.SafetyStop = delegate(string reason) { Post(delegate { StopAutomatic(); MessageBox.Show(reason, "已停止自动允许", MessageBoxButtons.OK, MessageBoxIcon.Warning); }); };
                watcher.ResultChanged = delegate { Post(UpdateState); };
                UpdateState();
                tray.ShowBalloonTip(5000, "ChromeDevToolsAutoAllow 1.0.0", settingsWarning != null ? "设置无法读取，已保持仅检测。请查看日志。" : "已启动：" + TrayMenu.ModeText(lease.Mode) + "。右键托盘可立即停止。", ToolTipIcon.Info);
            }
            watcher.Start();
            if (seconds > 0) duration = new System.Threading.Timer(delegate { try { marshal.BeginInvoke((Action)ExitThread); } catch { } }, null, seconds * 1000, Timeout.Infinite);
        }
        private void Post(Action action)
        { try { if (!marshal.IsDisposed) marshal.BeginInvoke(action); } catch (InvalidOperationException) { } }
        private void UpdateState()
        {
            if (tray == null) return;
            ApprovalMode mode = lease.Mode;
            tray.Text = "ChromeDevToolsAutoAllow · " + (watcher.NeedsManual ? "上次未确认，需手动授权" : mode == ApprovalMode.Permanent ? "永久自动允许" : mode == ApprovalMode.Temporary ? "10分钟自动允许" : "仅检测");
            TrayMenu.Update(menu, mode);
        }
        private void Enable(bool permanent)
        {
            string scope = permanent
                ? "永久模式没有时间限制，并保存到 EXE 同目录的 settings.ini；下次启动程序会自动恢复。退出程序只是暂停运行，不会清除该选择。\n\n若不想下次自动恢复，请点击“立即停止自动允许”。仍保留单次运行 256 次尝试的异常保护上限。"
                : "启用 10 分钟后自动失效；如果之前保存了永久模式，将取消其持久设置。";
            string warning = "这会自动允许 Chrome 原生远程调试请求，包括访问会话数据、Cookie 和控制网页。\n\n该对话框不显示请求者身份，不能判断请求是否来自 Chrome DevTools MCP；其他本机程序发起的同类请求也可能被允许。\n\n" + scope + "\n\n不会设置开机启动，也不会改变 Chrome 配置。确认由你启用？";
            if (MessageBox.Show(warning, permanent ? "确认启用永久自动允许" : "确认启用 10 分钟", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                if (permanent) modes.EnablePermanent(); else modes.EnableTemporary();
                expiry.Change(permanent ? Timeout.Infinite : 600050, Timeout.Infinite);
                log.Write("ARMED", permanent ? "User enabled PERMANENT mode; saved to settings.ini." : "User enabled 10-minute mode; persistent mode disabled.");
                UpdateState(); watcher.ScanExisting();
            }
            catch (Exception e) { SettingsFailure(e); }
        }
        private void StopAutomatic()
        {
            expiry.Change(Timeout.Infinite, Timeout.Infinite);
            try { modes.Stop(); log.Write("DISARMED", "User/runtime disabled automatic approval and cleared persistent mode."); UpdateState(); }
            catch (Exception e) { SettingsFailure(e); }
        }
        private void SettingsFailure(Exception e)
        {
            lease.Disarm(); UpdateState();
            log.Write("SETTINGS_ERROR", e.GetType().Name + ": " + e.Message);
            MessageBox.Show("当前已停止自动允许，但未能保存设置。\n\n" + e.Message + "\n\n重启前请确认 settings.ini 中 permanentAutoAllow=false，或移除此设置文件，否则旧的永久设置可能恢复。\n\n" + modes.Settings.Path, "设置保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        protected override void ExitThreadCore()
        {
            if (duration != null) duration.Dispose();
            expiry.Dispose();
            watcher.Dispose();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (trayIcon != null) trayIcon.Dispose();
            if (menu != null) menu.Dispose();
            marshal.Dispose(); base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        private static string Value(string[] args, string name, string fallback)
        {
            int i = Array.IndexOf(args, name); if (i < 0) return fallback;
            if (i + 1 >= args.Length) throw new ArgumentException("Missing value for " + name);
            return args[i + 1];
        }
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Contains("--menu-preview")) { TrayMenu.Preview(Value(args, "--menu-preview", null)); return 0; }
                if (args.Contains("--icon-diagnostics")) { Icons.Diagnostics(Value(args, "--icon-diagnostics", null)); return 0; }
                string[] valued = { "--chrome-path", "--log", "--seconds" };
                for (int i = 0; i < args.Length; i++)
                {
                    if (valued.Contains(args[i])) { if (++i >= args.Length) throw new ArgumentException("Missing argument value"); }
                    else if (args[i] != "--no-tray" && args[i] != "--observe") throw new ArgumentException("Unknown argument: " + args[i]);
                }
                int seconds = Int32.Parse(Value(args, "--seconds", "0"), CultureInfo.InvariantCulture);
                if (seconds < 0 || seconds > 86400) throw new ArgumentException("--seconds must be 0..86400");
                if (args.Contains("--no-tray") && seconds == 0) throw new ArgumentException("--no-tray requires a finite --seconds value.");
                string path = Value(args, "--chrome-path", null) ?? ProcessPolicy.FindChrome();
                if (!File.Exists(path) || !String.Equals(System.IO.Path.GetFileName(path), "chrome.exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("--chrome-path must be an existing chrome.exe");
                string logfile = Value(args, "--log", System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "guard-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"));
                bool created;
                using (var mutex = new Mutex(true, "Local\\ChromeConsentGuard.Observer.v1", out created))
                {
                    if (!created) { if (!args.Contains("--no-tray")) MessageBox.Show("程序已在运行。如需使用新版，请先从旧版托盘菜单退出，再启动新版。", "ChromeDevToolsAutoAllow 1.0.0"); return 2; }
                    using (var log = new Log(logfile))
                    using (var app = new TrayContext(new ProcessPolicy(path, 0), log, !args.Contains("--no-tray"), seconds, args.Contains("--observe"))) Application.Run(app);
                    mutex.ReleaseMutex();
                }
                return 0;
            }
            catch (Exception e)
            {
                string errorPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last-error.txt");
                try { File.WriteAllText(errorPath, e.ToString(), Encoding.UTF8); } catch { }
                if (!args.Contains("--no-tray")) MessageBox.Show(e.Message, "ChromeDevToolsAutoAllow error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
