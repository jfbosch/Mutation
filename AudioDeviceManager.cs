using System;
using System.Collections.Generic;
using System.Linq;

namespace YourNamespace
{
    public class YourClass
    {
        public void YourMethod(IEnumerable<string> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            foreach (var item in items)
            {
                Console.WriteLine(item);
            }
        }
    }
}