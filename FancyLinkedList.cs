using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVIndustry
{
    internal class FancyLinkedList<T> : LinkedList<T>
    {
        public FancyLinkedList() : base() { }
        public FancyLinkedList(IEnumerable<T> collection) : base(collection) { }

        public void RemoveFirstWhere(Func<T, bool> predicate)
        {
            LinkedListNode<T> curNode = First;
            while (curNode != null)
            {
                if (predicate(curNode.Value))
                {
                    Remove(curNode);
                    return;
                }

                curNode = curNode.Next;
            }
        }
    }
}
