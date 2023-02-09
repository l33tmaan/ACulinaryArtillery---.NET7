using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.MathTools;

namespace ACulinaryArtillery.Util {
    internal static class MathHelper {

        public static Vec3f Center(this Cuboidf cubeoid) {
            return new Vec3f(cubeoid.MidX, cubeoid.MidY, cubeoid.MidZ);
        }

        public static Vec2f XZSize(this Cuboidf cubeoid) {
            return new Vec2f(cubeoid.Width, cubeoid.Height);
        }

        public static Vec2f ToXZ(this Vec3f v)
            => new Vec2f(v.X, v.Z);


        public static void Deconstruct(this Vec2f v, out double a, out double b) {
            a = v.X;
            b = v.Y;
        }

        /// <summary>
        /// <para>
        /// Returns the intersection of a line from the point <paramref name="point"/>.<see cref=""/>
        /// (<paramref name="point"/>.<see cref="Vec3f.X">X</see>, <paramref name="point"/>.<see cref="Vec3f.Z">Z</see>) 
        /// going through the center of an ellipses at (<paramref name="box"/>.<see cref="Cuboidf.MidX">MidX</see>, <paramref name="box"/>.<see cref="Cuboidf.MidZ">MidZ</see>)
        /// with semiaxes <paramref name="box" />.<see cref="Cuboidf.Width">Width/2</see> &amp; <paramref name="box"/>.<see cref="Cuboidf.Length">Length/2</see> closest to
        /// <paramref name="point"/> in the X/Z plane at a height of <paramref name="box"/>.<see cref="Cuboidf.MaxY">MaxY</see>.
        /// </para>
        /// <para>In short terms: What's the point on the ellipses that's closest to <paramref name="point"/>.</para>
        /// </summary>
        /// <param name="box">Cube to use</param>
        /// <param name="point">Point to use</param>
        /// <seealso href="https://mathworld.wolfram.com/Ellipse-LineIntersection.html"/>
        public static Vec3d TopFaceEllipsesLineIntersection(this Cuboidf box, Vec3f point) {
            var (a, b) = box.XZSize() / 2;

            Vec3f cubeCenter = box.Center();
            var (x, z) = point.ToXZ() - cubeCenter.ToXZ();

            double ab = a * b;
            double d = Math.Sqrt(a * a * z * z + b * b * x * x);

            double f = ab / d;

            return new Vec3d(f * x, 0, f * z);
        }

    }
}
