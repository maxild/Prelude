using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Maxfire.Prelude.Linq
{
    public static class EnumerableExtensions
    {
        [DebuggerStepThrough]
        public static IEnumerable<TResult> Map<T, TResult>(this IEnumerable<T> values, Func<T, TResult> projection)
        {
            foreach (T item in values)
            {
                yield return projection(item);
            }
        }

        [DebuggerStepThrough]
        public static IEnumerable<TResult> Map<T, TResult>(this IEnumerable<T> values, Func<T, int, TResult> projectionWithIndex)
        {
            int index = 0;
            foreach (T item in values)
            {
                yield return projectionWithIndex(item, index);
                index++;
            }
        }

        [DebuggerStepThrough]
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public static IEnumerable Each(this IEnumerable values, Action<object?> eachAction)
        {
            foreach (var item in values)
            {
                eachAction(item);
            }

            return values;
        }

        [DebuggerStepThrough]
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public static IEnumerable Each(this IEnumerable values, Action<object?, int> eachAction)
        {
            int i = 0;
            foreach (var item in values)
            {
                eachAction(item, i);
                i++;
            }

            return values;
        }

        [DebuggerStepThrough]
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public static IEnumerable<T> Each<T>(this IEnumerable<T> values, Action<T> eachAction)
        {
            foreach (var item in values)
            {
                eachAction(item);
            }

            return values;
        }

        [DebuggerStepThrough]
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public static IEnumerable<T> Each<T>(this IEnumerable<T> values, Action<T, int> eachAction)
        {
            int i = 0;
            foreach (var item in values)
            {
                eachAction(item, i);
                i++;
            }

            return values;
        }
    }
}
