using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ACulinaryArtillery
{
    public static class ShapeUtilExtensions
    {
        public static Shape FlattenElementHierarchy(this Shape shape)
        {
            shape.Elements = [.. shape.Elements.FlattenHierarchy()];
            return shape;
        }

        private static IEnumerable<ShapeElement> FlattenHierarchy(this IEnumerable<ShapeElement> source)
        {
            foreach (var element in source)
            {
                yield return element;

                if (element.Children == null) continue;

                foreach (var child in element.Children.FlattenHierarchy())
                {
                    yield return child;
                }
            }
        }
    }
}