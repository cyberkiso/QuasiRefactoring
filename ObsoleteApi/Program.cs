using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObsoleteApi
{
    class Program
    {
        static void Main(string[] args)
        {
            //no warning highlighted
            var doService = new DoService();
            doService.DoSomething();
            //will be highlighted
            //IAppService service = doService as IAppService;
            //service.DoSomething();
            doService.Test();
        }

        static void Test()
        {
            var doService = new DoService();
            doService.DoSomething();
            doService.Test();
        }
    }
}