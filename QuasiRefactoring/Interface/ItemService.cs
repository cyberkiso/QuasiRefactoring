using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interface
{
    public class ItemService : IAppService
    {
        public List<string> GetItems()
        {
            return new List<string>()
            {"item1", "item2", "item3", };
        }
    }
}