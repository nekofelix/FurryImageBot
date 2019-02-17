using System;
using System.Collections.Generic;
using System.Linq;

namespace FurryImageBot
{
    public static class ExtensionMethods
    {
        public static IEnumerable<T> Interleave<T>(this IEnumerable<IEnumerable<T>> source)
        {
            IEnumerator<T>[] enumerators = source.Select(e => e.GetEnumerator()).ToArray();
            try
            {
                T[] elements;
                do
                {
                    elements = enumerators.Where(e => e.MoveNext()).Select(e => e.Current).ToArray();
                    foreach (T item in elements)
                    {
                        yield return item;
                    }
                }
                while (elements.Any());
            }
            finally
            {
                Array.ForEach(enumerators, e => e.Dispose());
            }
        }
    }
}
