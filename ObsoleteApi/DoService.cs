using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObsoleteApi
{
    public class DoService : IAppService
    {
        public void DoSomething()
        {
            Console.WriteLine("This is obsolete method. Use DoSomethingNew.");
        }

        public void DoSomethingNew()
        {
            Console.WriteLine("Good.");
        }

        public void Test()
        {
            Console.WriteLine("This is obsolete method. Use TestNew.");
        }

        public void TestNew()
        {
            Console.WriteLine("Good.");
        }
    }
}
