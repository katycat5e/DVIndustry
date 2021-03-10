using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace DVIndustry
{
    internal static class Utilities
    {
        private delegate List<TrainCar> GetCarsFromIdDelegate( string[] guids );
        private static readonly GetCarsFromIdDelegate getTrainCarsFromCarGuids;

        public static List<TrainCar> GetTrainCarsFromCarGuids( string[] carGuids )
        {
            return getTrainCarsFromCarGuids?.Invoke(carGuids);
        }

        static Utilities()
        {
            getTrainCarsFromCarGuids = AccessTools.Method("JobSaveManager.GetTrainCarsFromCarGuids")?
                .CreateDelegate(typeof(GetCarsFromIdDelegate)) as GetCarsFromIdDelegate;
        }
    }

    internal class FancyLinkedList<T> : LinkedList<T>
    {
        public FancyLinkedList() : base() { }
        public FancyLinkedList( IEnumerable<T> collection ) : base(collection) { }

        public void RemoveFirstWhere( Func<T, bool> predicate )
        {
            LinkedListNode<T> curNode = First;
            while( curNode != null )
            {
                if( predicate(curNode.Value) )
                {
                    Remove(curNode);
                    return;
                }

                curNode = curNode.Next;
            }
        }
    }
}
