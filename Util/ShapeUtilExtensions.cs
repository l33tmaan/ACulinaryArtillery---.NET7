using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace ACulinaryArtillery
{
    public static class ShapeUtilExtensions
    {

        public static Shape RemoveReflective(this Shape shape)
        {
            foreach (var element in shape.Elements)
            {
                foreach (var face in element.FacesResolved)
                {
                    face.ReflectiveMode = 0;
                }
            }
            return shape;
        }

        public static Shape FlattenHierarchy(this Shape shape)
        {
            shape.Elements = FlattenHierarchy(shape.Elements).ToArray();
            return shape;
        }

        public static List<ShapeElement> FlattenHierarchy(ShapeElement[] rootElements)
        {
            var flatList = new List<ShapeElement>();
            foreach (var element in rootElements)
            {
                FlattenHierarchyHelper(element, flatList);
            }
            return flatList;
        }

        private static void FlattenHierarchyHelper(ShapeElement element, List<ShapeElement> flatList)
        {
            flatList.Add(element);
            if (element.Children != null)
            {
                foreach (var child in element.Children)
                {
                    FlattenHierarchyHelper(child, flatList);
                }
            }
        }
    }
}
