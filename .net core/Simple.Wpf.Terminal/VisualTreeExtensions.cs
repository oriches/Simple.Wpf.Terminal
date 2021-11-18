using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Simple.Wpf.Terminal
{
    public static class VisualTreeExtensions
    {
        public static T GetVisualAncestor<T>(this DependencyObject d) where T : class
        {
            var item = VisualTreeHelper.GetParent(d);

            while (item != null)
            {
                if (item is T itemAsT) return itemAsT;
                item = VisualTreeHelper.GetParent(item);
            }

            return null;
        }

        public static DependencyObject GetVisualAncestor(this DependencyObject d, Type type)
        {
            var item = VisualTreeHelper.GetParent(d);

            while (item != null)
            {
                if (item.GetType() == type) return item;
                item = VisualTreeHelper.GetParent(item);
            }

            return null;
        }

        public static T GetVisualDescendent<T>(this DependencyObject d) where T : DependencyObject
        {
            return d.GetVisualDescendents<T>()
                .FirstOrDefault();
        }

        public static IEnumerable<T> GetVisualDescendents<T>(this DependencyObject d) where T : DependencyObject
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