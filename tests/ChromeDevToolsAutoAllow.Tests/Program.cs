using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ChromeDevToolsAutoAllow
{
    internal static class TestProgram
    {
        private static string Value(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing value for " + key);
            return args[index + 1];
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Contains("--fixture"))
                {
                    Tests.Fixture(Value(args, "--fixture"), args.Contains("--fixture-stays-open"), args.Contains("--fixture-delayed-close"), args.Contains("--fixture-disable-button"));
                    return 0;
                }
                if (args.Contains("--settings-probe"))
                {
                    var modes = new ModeController(new Lease(), new SettingsStore(Value(args, "--settings-probe"), Value(args, "--target")));
                    modes.Restore(args.Contains("--observe"));
                    File.WriteAllText(Value(args, "--probe-out"), modes.Lease.Mode.ToString());
                    return 0;
                }
                bool full = args.Contains("--self-test");
                if (!full && !args.Contains("--unit-test")) throw new ArgumentException("Use --unit-test <directory> or --self-test <directory>.");
                return Tests.Run(Value(args, full ? "--self-test" : "--unit-test"), full);
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
    }
}
