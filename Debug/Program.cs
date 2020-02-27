using System;
using System.Collections.Generic;
using MECore;

namespace Debug
{
    class Program
    {
        static void Main(string[] args)
        {
            IniFile IF = new IniFile("input.mini", true);
            IF.SaveAs("output.mini", System.IO.FileAccess.ReadWrite, System.IO.FileMode.Create);
        }
    }
}
