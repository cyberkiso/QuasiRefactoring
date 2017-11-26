using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interface
{
    public static class Checker
    {
        public static void CheckSomething()
        {
            Console.WriteLine("CheckSomething");
        }
    }

    public class Program
    {
        static void Main(string[] args)
        {
            var service = new ItemService();
            var items = service.GetItems();
        }
    }
}
