using System;
using System.Windows.Forms;

namespace ctr_PatcherConsole
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                PrintReadme();
                foreach (string path in args)
                {
                    PatchFile(path);
                }
            }
            else
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "CCI Files|*.3ds;*.cci;*.3dz";
                dialog.Multiselect = true;
                dialog.Title = "Please select a 3DS ROM file.";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    PrintReadme();
                    foreach (string path in dialog.FileNames)
                    {
                        PatchFile(path);
                    }
                }
            }
        }
        public static void PatchFile(string path)
        {
            try
            {
                Patch patch = new Patch(Properties.Resources.Patch, path);
                patch.ApplyPatch();
                MessageBox.Show("Done!", "Message");
            }
            catch (Exception e)
            {
                Console.Write("Failed");
                MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        public static void PatchHook() { }
        public static void PrintReadme()
        {
            Console.Clear();
            Console.Write(Properties.Resources.Readme);
            Console.Write("\nPress Any Key...");
            Console.ReadKey(true);
            Console.Clear();
        }
    }
}
