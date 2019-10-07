namespace mprFastViewCut
{
    using System;
    using System.Diagnostics;
    using Autodesk.Revit.DB;

    public static class GeometryHelpers
    {
        // http://thebuildingcoder.typepad.com/blog/2014/09/planes-projections-and-picking-points.html
        /// <summary>
        /// Return signed distance from plane to a given point.
        /// </summary>
        public static double SignedDistanceTo(this Plane plane, XYZ p)
        {
            Debug.Assert(
                IsEqual(plane.Normal.GetLength(), 1),
                "expected normalized plane normal");

            var v = p - plane.Origin;
            return plane.Normal.DotProduct(v);
        }

        /// <summary>
        /// Project given 3D XYZ point onto plane.
        /// </summary>
        public static XYZ ProjectOnto(this Plane plane, XYZ p)
        {
            var d = plane.SignedDistanceTo(p);

            var q = p - d * plane.Normal;

            Debug.Assert(
                IsZero(plane.SignedDistanceTo(q)),
                "expected point on plane to have zero distance to plane");

            return q;
        }

        public static bool IsZero(double a, double tolerance)
        {
            return tolerance > Math.Abs(a);
        }

        public static bool IsZero(double a)
        {
            return IsZero(a, Eps);
        }

        public static bool IsEqual(double a, double b)
        {
            return IsZero(b - a);
        }
        private const double Eps = 1.0e-9;
    }
}
