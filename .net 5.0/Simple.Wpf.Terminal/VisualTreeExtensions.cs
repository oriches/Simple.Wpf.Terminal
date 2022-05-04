using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Simple.Wpf.Terminal
{
    /// <summary>
    ///     Extension methods for querying the Visual Tree
    /// </summary>
    internal static class VisualTreeExtensions
    {
        /// <summary>
        ///     Search the Visual Tree for an Ancestor by Type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        internal static T GetVisualAncestor<T>(this DependencyObject d) where T : class
        {
            var item = VisualTreeHelper.GetParent(d);

            while (item != null)
            {
                if (item is T itemAsT) return itemAsT;
                item = VisualTreeHelper.GetParent(item);
            }

            return null;
        }

        /// <summary>
        ///     Search the Visual Tree for an Ancestor
        /// </summary>
        /// <param name="d"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static DependencyObject GetVisualAncestor(this DependencyObject d, Type type)
        {
            var item = VisualTreeHelper.GetParent(d);

            while (item != null)
            {
                if (item.GetType() == type) return item;
                item = VisualTreeHelper.GetParent(item);
            }

            return null;
        }

        /// <summary>
        ///     Search the Visual Tree for a Descendent (child) by Type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        internal static T GetVisualDescendent<T>(this DependencyObject d) where T : DependencyObject
        {
            return d.GetVisualDescendents<T>()
                .FirstOrDefault();
        }

        /// <summary>
        ///     Search the Visual Tree for multiple Descendents (children) by Type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        internal static IEnumerable<T> GetVisualDescendents<T>(this DependencyObject d) where T : DependencyObject
        {
            var childCount = VisualTreeHelper.GetChildrenCount(d);

            for (var n = 0; n < childCount; n++)
            {
                var child = VisualTreeHelper.GetChild(d, n);

                if (child is T dependencyObject) yield return dependencyObject;

                foreach (var match in GetVisualDescendents<T>(child)) yield return match;
            }
        }
    }
}