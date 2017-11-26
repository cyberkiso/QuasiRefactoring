using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interface
{
    public class SampleService : IAppService
    {
        public List<string> GetItems()
        {
            return new List<string>()
            {"sample1", "sample2", "sample3", };
        }
    }
}