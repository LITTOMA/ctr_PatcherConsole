using System.Text;

namespace ctr_PatcherConsole
{
    class ConsoleProcessBar
    {
        public ConsoleProcessBar()
        {
            BarLength = 30;
        }

        public long Maximum { get; set; }
        public long Minimum { get; set; }
        public long Value { get; set; }
        public int BarLength { get; set; }
        public int Percent
        {
            get { return (int)((Value - Minimum) / (float)(Maximum - Minimum) * 100); }
        }
        public string Bar
        {
            get
            {
                int entity = (int)((float)Percent / 100 * BarLength);
                int blank = BarLength - entity;
                return string.Format("[{0}{1}]", GeneralString(entity, "="), GeneralString(blank, " "));
            }
        }
        public new string ToString()
        {
            return Bar;
        }

        private string GeneralString(int n, string par)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                sb.Append(par);
            }
            return sb.ToString();
        }
    }
}
