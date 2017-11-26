using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObsoleteApi
{
    public class Test
    {
        public IAppService service
        {
            get;
            set;
        }

        public Test()
        {
            service = new DoService();
            service.DoSomething();
            service.Test();
        }
    }
}