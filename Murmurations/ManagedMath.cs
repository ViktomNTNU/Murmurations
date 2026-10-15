using Vec3 = System.Numerics.Vector3;

namespace Murmurations
{
    // In The Long Dark every UnityEngine.Vector3 and Mathf operation, even + or new Vector3(...), is a call into the
    // game's native code that also leaves an object for the game's garbage collector. These are plain C# versions with the
    // same results as Unity's, for code that runs for every bird. Unity types are only built where they are handed to Unity.
    internal static class ManagedMath
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }

        public static float Sin(float value) => (float)Math.Sin(value);

        public static int FloorToInt(float value) => (int)Math.Floor(value);

        public static int CeilToInt(float value) => (int)Math.Ceiling(value);

        public static Vec3 Normalized(Vec3 value)
        {
            float magnitude = value.Length();
            return magnitude > 1E-05f ? value / magnitude : Vec3.Zero;
        }

        public static Vec3 ClampMagnitude(Vec3 value, float maxLength)
        {
            float sqrMagnitude = value.LengthSquared();
            if (sqrMagnitude <= maxLength * maxLength) return value;

            return value / MathF.Sqrt(sqrMagnitude) * maxLength;
        }

        public static Vec3 ProjectOnPlane(Vec3 vector, Vec3 planeNormal)
        {
            float sqrMagnitude = planeNormal.LengthSquared();
            if (sqrMagnitude < float.Epsilon) return vector;

            return vector - planeNormal * (Vec3.Dot(vector, planeNormal) / sqrMagnitude);
        }

        public static Vec3 ToNumerics(this Vector3 value) => new(value.x, value.y, value.z);

        // Sets the fields directly, because the Vector3 constructor is itself a call into the game.
        public static Vector3 ToUnity(this Vec3 value)
        {
            Vector3 result = default;
            result.x = value.X;
            result.y = value.Y;
            result.z = value.Z;
            return result;
        }
    }
}
