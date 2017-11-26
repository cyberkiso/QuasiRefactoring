using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObsoleteApi
{
    public interface IAppService
    {
        [Obsolete("Use DoSomethingNew")]
        void DoSomething();

        void DoSomethingNew();

        [Obsolete("Use TestNew")]
        void Test();

        void TestNew();
    }
}
