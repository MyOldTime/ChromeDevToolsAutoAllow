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
    internal static class Tests
    {
        private static int passed;
        private static Log log;
        private static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); passed++; log.Write("PASS", name); }
        private static Node N(string name, int type, params Node[] children)
        {
            var n = new Node { Name = name, Type = type, Pid = 123 }; n.Children.AddRange(children); return n;
        }
        private static Node Example(bool zh)
        {
            return N(zh ? "要允许远程调试吗？" : "Allow remote debugging?", ControlType.Window.Id,
                N(zh ? "外部应用请求完全控制；包括 Cookie。请只使用受信任的应用。" : "An external app requests full control, including cookies. Use trusted apps only.", ControlType.Text.Id),
                N(zh ? "允许" : "Allow", ControlType.Button.Id), N(zh ? "取消" : "Cancel", ControlType.Button.Id));
        }
        internal static int Run(string folder, bool includeIntegration)
        {
            Directory.CreateDirectory(folder);
            using (log = new Log(System.IO.Path.Combine(folder, "self-test.log")))
            {
                try
                {
                    Check(Matcher.Find(Example(false), 123).Count == 1, "English exact dialog");
                    Check(Matcher.Find(Example(true), 123).Count == 1, "Chinese exact dialog");
                    Node n = Example(false); n.Name = "Allow camera?"; Check(Matcher.Find(n, 123).Count == 0, "Reject unrelated permission");
                    n = Example(false); n.Children[0].Name = "A random Allow button"; Check(Matcher.Find(n, 123).Count == 0, "Require full-control warning tokens");
                    n = Example(false); n.Children[1].Enabled = false; Check(Matcher.Find(n, 123).Count == 0, "Reject disabled Allow");
                    n = Example(false); n.Children[1].Visible = false; Check(Matcher.Find(n, 123).Count == 0, "Reject hidden Allow");
                    n = Example(false); n.Children.Add(N("Allow", ControlType.Button.Id)); Check(Matcher.Find(n, 123).Count == 0, "Reject ambiguous Allow buttons");
                    n = Example(false); n.Children.RemoveAt(2); Check(Matcher.Find(n, 123).Count == 0, "Require Cancel button");
                    n = Example(false); n.Children[1].Pid = 124; Check(Matcher.Find(n, 123).Count == 0, "Reject mixed process tree");
                    n = N("page", ControlType.Document.Id, Example(false)); Check(Matcher.Find(n, 123).Count == 0, "Reject webpage ARIA spoof");
                    n = Example(false); n.Visible = false; Check(Matcher.Find(n, 123).Count == 0, "Reject invisible dialog");
                    n = Example(false); n.Children[1].Name = "Always Allow"; Check(Matcher.Find(n, 123).Count == 0, "Require exact Allow name");
                    n = Example(false); n.Children.Insert(0, N(n.Name, ControlType.Text.Id)); n.Name = ""; Check(Matcher.Find(n, 123).Count == 1, "Support native title label");
                    n = N("browser", ControlType.Window.Id, Example(false), Example(true)); Check(Matcher.Find(n, 123).Count == 2, "Ambiguous multiple dialogs remain ambiguous");
                    Check(Identity.Of(123, "42.1.2") == Identity.Of(123, "42.1.2"), "Same UIA button through different HWNDs shares one dedup key");
                    Check(Identity.Of(123, "42.1.2") != Identity.Of(124, "42.1.2"), "Dedup separates processes");
                    Check(Identity.Of(123, "42.1.2") != Identity.Of(123, "42.1.3"), "Dedup separates distinct buttons");
                    var lease = new Lease(); Check(!lease.Active, "Default is observation only"); lease.Arm(1); Check(lease.Active, "Explicit lease arms"); lease.Disarm(); Check(!lease.Active, "Disarm is immediate");
                    lease.Arm(0); Check(!lease.Active, "Expired lease cannot approve");
                    IconTests();
                    PermanentModeTests(folder);
                    VerificationTests();
                    if (includeIntegration)
                    {
                    Integration(folder, ApprovalMode.Observe);
                    Integration(folder, ApprovalMode.Temporary);
                    Integration(folder, ApprovalMode.Permanent);
                    Integration(folder, ApprovalMode.Temporary, "close");
                    Integration(folder, ApprovalMode.Temporary, "delayed-close");
                    Integration(folder, ApprovalMode.Temporary, "disable-button");
                    }
                    log.Write("RESULT", "PASS; checks=" + passed + "; suite=" + (includeIntegration ? "full" : "unit") + "; real Chrome permissions were NOT clicked");
                    return 0;
                }
                catch (Exception e) { log.Write("RESULT", e.ToString()); return 1; }
            }
        }
        private static void VerificationTests()
        {
            int reads = 0, elapsed = 0;
            var result = DialogVerifier.Observe(delegate { reads++; return DialogObservation.Visible; }, delegate(int ms) { elapsed += ms; return false; });
            Check(result == DialogObservation.Visible && reads == 4 && elapsed == 3250, "Visible dialog remains unconfirmed after four bounded READ-ONLY checks");
            reads = 0; elapsed = 0;
            result = DialogVerifier.Observe(delegate { reads++; return reads < 3 ? DialogObservation.Visible : DialogObservation.Gone; }, delegate(int ms) { elapsed += ms; return false; });
            Check(result == DialogObservation.Gone && reads == 3 && elapsed == 1750, "Delayed disappearance is observed beyond the old 350ms check");
            reads = 0;
            result = DialogVerifier.Observe(delegate { reads++; throw new InvalidOperationException("test provider read failure"); }, delegate(int ms) { return false; });
            Check(result == DialogObservation.Unavailable && reads == 4, "Provider read errors are unknown, never successful authorization");
            reads = 0;
            result = DialogVerifier.Observe(delegate { reads++; return DialogObservation.Gone; }, delegate(int ms) { return true; });
            Check(result == DialogObservation.Cancelled && reads == 0, "Stop cancels verification before another provider read");
            reads = 0;
            result = DialogVerifier.Observe(delegate { reads++; return DialogObservation.Unavailable; }, delegate(int ms) { return reads == 1; });
            Check(result == DialogObservation.Cancelled && reads == 1, "Stop interrupts a pending verification sequence");
            reads = 0;
            result = DialogVerifier.Observe(delegate { reads++; return reads < 4 ? DialogObservation.Unavailable : DialogObservation.Visible; }, delegate(int ms) { return false; });
            Check(result == DialogObservation.Visible, "A recovered but still visible container remains unconfirmed");
        }
        private static void IconTests()
        {
            foreach (string resource in new string[] { Icons.AppResource, Icons.TrayResource })
            {
                using (var stream = Icons.Open(resource))
                using (var reader = new BinaryReader(stream))
                {
                    Check(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1, resource + ": valid ICO header");
                    int count = reader.ReadUInt16();
                    Check(count == (resource == Icons.AppResource ? 10 : 7), resource + ": expected resolution count");
                    for (int i = 0; i < count; i++)
                    {
                        int width = reader.ReadByte(), height = reader.ReadByte(); reader.ReadByte(); reader.ReadByte();
                        int planes = reader.ReadUInt16(), depth = reader.ReadUInt16(); int length = reader.ReadInt32(), offset = reader.ReadInt32();
                        if (width != height || planes != 1 || depth != 32 || offset < 6 + 16 * count || length <= 0 || offset + length > stream.Length)
                            throw new Exception("Invalid ICO frame in " + resource);
                    }
                    Check(true, resource + ": every frame is bounded and 32-bit");
                }
                foreach (int size in new int[] { 16, 20, 24, 32, 48, 64 })
                using (var icon = Icons.Load(resource, new Size(size, size)))
                using (var bitmap = icon.ToBitmap())
                {
                    Check(icon.Width == size && icon.Height == size && bitmap.GetPixel(0, 0).A == 0, resource + ": " + size + "px loads at exact size with transparency");
                }
            }
            using (var app = Icons.Load(Icons.AppResource, new Size(256, 256)))
            using (var bitmap = app.ToBitmap()) Check(bitmap.Width == 256 && bitmap.GetPixel(0, 0).A == 0, "App 256px PNG icon frame loads with alpha");
            using (var tray = Icons.Tray())
            using (var notify = new NotifyIcon { Icon = tray })
                Check(notify.Icon != null && notify.Icon.Size == SystemInformation.SmallIconSize, "NotifyIcon uses the dedicated system-small-size tray resource");
            using (var actual = Icon.ExtractAssociatedIcon(typeof(Icons).Assembly.Location))
            using (var expected = Icons.Load(Icons.AppResource, actual.Size))
            using (var a = actual.ToBitmap())
            using (var e = expected.ToBitmap())
            {
                long difference = 0;
                for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++)
                {
                    Color ca = a.GetPixel(x, y), ce = e.GetPixel(x, y);
                    difference += Math.Abs(ca.A - ce.A);
                    if (ca.A > 0 && ce.A > 0) difference += Math.Abs(ca.R - ce.R) + Math.Abs(ca.G - ce.G) + Math.Abs(ca.B - ce.B);
                }
                Check(difference < a.Width * a.Height * 8L, "Actual Windows EXE icon matches the original app artwork");
            }
        }
        private static void PermanentModeTests(string folder)
        {
            string config = System.IO.Path.Combine(folder, "test-settings.ini");
            string target = System.IO.Path.Combine(folder, "mock-chrome.exe");
            long tick = 0;
            var lease = new Lease(delegate { return tick; });
            var store = new SettingsStore(config, target);
            var modes = new ModeController(lease, store);
            string warning;
            Check(modes.Restore(false) == null && lease.Mode == ApprovalMode.Observe, "New install remains observe-only");
            Check(!File.Exists(config), "Reading missing settings does not write or opt in");
            modes.EnablePermanent();
            Check(lease.Mode == ApprovalMode.Permanent && store.Load(out warning), "Permanent selection saves and enables");
            tick += 20L * 365 * 24 * 60 * 60 * 1000;
            Check(lease.Mode == ApprovalMode.Permanent && lease.Active, "Permanent mode survives 20 simulated years without expiry");
            var restarted = new ModeController(new Lease(), new SettingsStore(config, target));
            Check(restarted.Restore(false) == null && restarted.Lease.Mode == ApprovalMode.Permanent, "Startup restores saved permanent mode");
            Check(ProbeSettings(config, target, folder, false) == "Permanent", "A separate new process restores permanent mode");
            Check(ProbeSettings(config, target, folder, true) == "Observe", "Explicit observe prevents restoration in a new process");
            modes.Restore(true);
            Check(!lease.Active && store.Load(out warning), "Observe override does not erase user's saved preference");
            var different = new ModeController(new Lease(), new SettingsStore(config, System.IO.Path.Combine(folder, "other-chrome.exe")));
            Check(different.Restore(false) != null && !different.Lease.Active, "Saved setting cannot silently authorize a different Chrome path");
            modes.EnableTemporary();
            Check(lease.Mode == ApprovalMode.Temporary && !store.Load(out warning), "Switching to 10 minutes clears persistent opt-in");
            tick += 600001;
            Check(!lease.Active, "Temporary mode still expires after 10 minutes");
            Check(ProbeSettings(config, target, folder, false) == "Observe", "Temporary mode is not restored after restart");
            modes.EnablePermanent(); modes.Stop();
            Check(lease.Mode == ApprovalMode.Observe && !store.Load(out warning), "Stop immediately disables and clears permanent setting");
            Check(ProbeSettings(config, target, folder, false) == "Observe", "Stopped permanent mode stays off after process restart");
            File.WriteAllText(config, "permanentAutoAllow=true");
            Check(modes.Restore(false) != null && !lease.Active, "Malformed settings fail closed");
            File.WriteAllText(config, new string('x', 5000));
            Check(modes.Restore(false) != null && !lease.Active, "Oversized settings fail closed");
            modes.EnablePermanent();
            using (var held = new FileStream(config, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool failed = false;
                try { modes.EnablePermanent(); } catch (IOException) { failed = true; }
                Check(failed && !lease.Active, "Failure to persist an opt-in leaves automatic approval off");
                lease.ArmPermanent(); failed = false;
                try { modes.Stop(); } catch (IOException) { failed = true; }
                Check(failed && !lease.Active, "Stop is immediate even when its settings write fails");
            }
            modes.Stop();
            Check(!store.Load(out warning), "Stop saves successfully after file access recovers");
            Check(Directory.GetFiles(folder, "test-settings.ini.*.tmp").Length == 0, "Settings replacement cleans temporary files");
            using (var menu = TrayMenu.Create(null, null, null, null, null, null))
            {
                Check(menu.Items.Count == 6 && menu.Items[2].Text == TrayMenu.PermanentLabel, "Tray contains the new permanent item directly after 10-minute mode");
                TrayMenu.Update(menu, ApprovalMode.Permanent);
                Check(((ToolStripMenuItem)menu.Items[2]).Checked && !((ToolStripMenuItem)menu.Items[1]).Checked, "Permanent mode displays its own check mark");
                TrayMenu.Update(menu, ApprovalMode.Temporary);
                Check(!((ToolStripMenuItem)menu.Items[2]).Checked && ((ToolStripMenuItem)menu.Items[1]).Checked, "Timed and permanent check marks are mutually exclusive");
                TrayMenu.Update(menu, ApprovalMode.Observe);
                Check(!((ToolStripMenuItem)menu.Items[2]).Checked && !((ToolStripMenuItem)menu.Items[1]).Checked, "Observe clears both mode check marks");
            }
        }
        private static string ProbeSettings(string config, string target, string folder, bool observe)
        {
            string output = System.IO.Path.Combine(folder, "settings-probe-" + Guid.NewGuid().ToString("N") + ".txt");
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            var psi = new ProcessStartInfo(exe, "--settings-probe \"" + config + "\" --target \"" + target + "\" --probe-out \"" + output + "\"" + (observe ? " --observe" : ""))
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            using (var process = Process.Start(psi))
            {
                if (!process.WaitForExit(5000)) { process.Kill(); throw new Exception("Settings probe timed out"); }
                if (process.ExitCode != 0) throw new Exception("Settings probe failed");
            }
            return File.ReadAllText(output).Trim();
        }
        private static void Integration(string folder, ApprovalMode mode)
        { Integration(folder, mode, mode == ApprovalMode.Observe ? "close" : "stay"); }
        private static void Integration(string folder, ApprovalMode mode, string behavior)
        {
            bool arm = mode != ApprovalMode.Observe;
            bool stays = behavior == "stay" || behavior == "disable-button";
            string label = mode + "/" + behavior;
            string marker = System.IO.Path.Combine(folder, mode.ToString() + "-" + behavior + "-" + Guid.NewGuid().ToString("N") + ".marker");
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string extra = stays ? " --fixture-stays-open" : "";
            if (behavior == "delayed-close") extra += " --fixture-delayed-close";
            if (behavior == "disable-button") extra += " --fixture-disable-button";
            var psi = new ProcessStartInfo(exe, "--fixture \"" + marker + "\"" + extra) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            using (var child = Process.Start(psi))
            {
              try
              {
                var lease = new Lease();
                var store = new SettingsStore(marker + ".settings.ini", exe);
                var modes = new ModeController(lease, store);
                if (mode == ApprovalMode.Permanent) modes.EnablePermanent(); else if (mode == ApprovalMode.Temporary) modes.EnableTemporary();
                using (var watcher = new Watcher(new ProcessPolicy(exe, child.Id), lease, log))
                {
                    int safetyStops = 0;
                    watcher.SafetyStop = delegate(string reason) { modes.Stop(); Interlocked.Increment(ref safetyStops); };
                    // Exercise actual cross-process WinEvent delivery + real UIA, not a mocked callback.
                    watcher.Start();
                    var elapsed = Stopwatch.StartNew();
                    while (elapsed.ElapsedMilliseconds < 12000)
                    {
                        Application.DoEvents();
                        if (arm ? Interlocked.Read(ref watcher.Unconfirmed) + Interlocked.Read(ref watcher.Dismissed) > 0 : Interlocked.Read(ref watcher.Detected) > 0) break;
                        Thread.Sleep(20); // TEST HARNESS ONLY; production listener blocks on events.
                    }
                    var settle = Stopwatch.StartNew(); while (settle.ElapsedMilliseconds < 650) { Application.DoEvents(); Thread.Sleep(20); }
                    Check(Interlocked.Read(ref watcher.Events) > 0, label + ": real WinEvent callback received");
                    Check(Interlocked.Read(ref watcher.Detected) > 0, label + ": real cross-process UIA dialog recognized");
                    Check(arm ? File.Exists(marker) : !File.Exists(marker), arm ? "Fixture Allow invoked, marker verifies handler ran" : "Observe mode does not invoke fixture Allow");
                    Check(arm ? Interlocked.Read(ref watcher.Invoked) == 1 : Interlocked.Read(ref watcher.Invoked) == 0, label + ": API-return count " + (arm ? "one" : "zero"));
                    if (arm) Check(File.ReadAllText(marker).Trim() == "1", "Repeated real events do not invoke the same fixture button twice");
                    Check(Interlocked.Read(ref watcher.Attempts) == (arm ? 1 : 0), label + ": no input retry was added");
                    if (arm && stays)
                    {
                        string warning;
                        Check(Interlocked.Read(ref watcher.Unconfirmed) == 1 && watcher.NeedsManual, label + ": non-effective call requests manual intervention");
                        Check(Interlocked.Read(ref watcher.Dismissed) == 0, label + ": remaining/disabled-button dialog is NOT reported as dismissed");
                        Check(!lease.Active && Volatile.Read(ref safetyStops) == 1, label + ": unconfirmed result immediately pauses automatic approval");
                        Check(!store.Load(out warning), label + ": safety stop persists off in the TEST settings only");
                        Check(watcher.LastResult.Contains("手动") && watcher.LastResult.Contains("没有确认"), label + ": status explains unknown result and required manual action");
                    }
                    else if (arm)
                    {
                        Check(Interlocked.Read(ref watcher.Dismissed) == 1 && Interlocked.Read(ref watcher.Unconfirmed) == 0, label + ": disappearance is observed without false timeout");
                        Check(!watcher.NeedsManual && Volatile.Read(ref safetyStops) == 0, label + ": no spurious safety stop");
                        Check(watcher.LastResult.Contains("不证明"), label + ": disappearance never claims MCP connection success");
                    }
                }
              }
              finally
              {
                if (!child.HasExited) { child.CloseMainWindow(); if (!child.WaitForExit(2000)) child.Kill(); }
              }
            }
        }
        internal static void Fixture(string marker, bool staysOpen, bool delayedClose, bool disableButton)
        {
            using (var form = new Form { Text = "Allow remote debugging?", Size = new Size(540, 230), StartPosition = FormStartPosition.CenterScreen, ShowInTaskbar = false })
            {
                var text = new Label { Text = "LOCAL TEST FIXTURE ONLY\nAn external app requests full control, including cookies.\nUse trusted apps only. No browser permission is granted.", Location = new Point(20, 20), Size = new Size(490, 85) };
                var yes = new Button { Text = "Allow", Location = new Point(315, 125), Size = new Size(90, 30) };
                var no = new Button { Text = "Cancel", Location = new Point(415, 125), Size = new Size(90, 30) };
                var closeLater = new System.Windows.Forms.Timer { Interval = 1100 };
                closeLater.Tick += delegate { closeLater.Stop(); form.Close(); };
                int clicks = 0;
                yes.Click += delegate
                {
                    File.WriteAllText(marker, (++clicks).ToString(CultureInfo.InvariantCulture));
                    if (disableButton) yes.Enabled = false;
                    if (delayedClose) closeLater.Start();
                    else if (!staysOpen) form.Close();
                };
                no.Click += delegate { form.Close(); };
                form.Controls.AddRange(new Control[] { text, yes, no });
                var timer = new System.Windows.Forms.Timer { Interval = 16000 };
                timer.Tick += delegate { form.Close(); }; timer.Start();
                var pulse = new System.Windows.Forms.Timer { Interval = 350 };
                pulse.Tick += delegate { form.Text = "Local fixture event pulse"; form.Text = "Allow remote debugging?"; };
                if (staysOpen) pulse.Start();
                // Give the parent listener time to install before this test window appears.
                Thread.Sleep(700);
                Application.Run(form); timer.Dispose(); pulse.Dispose(); closeLater.Dispose();
            }
        }
    }

}
