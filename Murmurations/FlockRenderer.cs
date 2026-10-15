using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.Rendering;
using Vec3 = System.Numerics.Vector3;

namespace Murmurations
{
    // Draws a whole murmuration as one mesh with a quad per bird, instead of a GameObject per bird. Moving, turning and
    // scaling a GameObject are each a call into the game, for every bird on every update; this mesh is uploaded in one call.
    // There is a submesh per animation frame. Every bird has a quad in each of them, and the quads of the frames a bird
    // isn't showing are collapsed to a point, so they draw nothing.
    internal sealed class FlockRenderer
    {
        private GameObject? m_Object;
        private MeshRenderer? m_Renderer;
        private Mesh? m_Mesh;
        private Il2CppStructArray<Vector3>? m_Vertices;
        private Material[]? m_Materials;
        private int m_Capacity;
        private int m_FrameCount;
        private int m_ShownBirdCount;

        // Builds the mesh when it is missing or the materials or the number of birds it holds change.
        // Returns false when there is nothing to draw with.
        public bool Prepare(Transform parent, Material[]? materials, int capacity, Vec3 hidePosition)
        {
            if (materials == null || materials.Length == 0 || capacity <= 0) return false;
            if (m_Object != null && m_Mesh != null && ReferenceEquals(materials, m_Materials) && capacity == m_Capacity) return true;

            Build(parent, materials, capacity, hidePosition);
            return true;
        }

        public void SetBird(int index, Vec3 center, Vec3 halfRight, Vec3 halfUp, int frame)
        {
            if (m_Vertices == null || index >= m_Capacity) return;

            for (int f = 0; f < m_FrameCount; f++)
            {
                int vertex = (f * m_Capacity + index) * 4;

                if (f != frame)
                {
                    CollapseQuad(vertex, center);
                    continue;
                }

                m_Vertices[vertex] = (center - halfRight - halfUp).ToUnity();
                m_Vertices[vertex + 1] = (center + halfRight - halfUp).ToUnity();
                m_Vertices[vertex + 2] = (center - halfRight + halfUp).ToUnity();
                m_Vertices[vertex + 3] = (center + halfRight + halfUp).ToUnity();
            }
        }

        // Sends the quads to the game. Quads of birds that were removed are hidden at hidePosition.
        public void Upload(int birdCount, Vec3 hidePosition)
        {
            if (m_Mesh == null || m_Vertices == null) return;

            for (int i = birdCount; i < m_ShownBirdCount && i < m_Capacity; i++) HideBird(i, hidePosition);
            m_ShownBirdCount = Math.Min(birdCount, m_Capacity);

            m_Mesh.vertices = m_Vertices;
            m_Mesh.RecalculateBounds();
        }

        public void Destroy()
        {
            if (m_Object != null) UnityEngine.Object.Destroy(m_Object);
            if (m_Mesh != null) UnityEngine.Object.Destroy(m_Mesh);

            m_Object = null;
            m_Renderer = null;
            m_Mesh = null;
            m_Vertices = null;
            m_Materials = null;
            m_Capacity = 0;
            m_FrameCount = 0;
            m_ShownBirdCount = 0;
        }

        private void Build(Transform parent, Material[] materials, int capacity, Vec3 hidePosition)
        {
            if (m_Object == null || m_Renderer == null)
            {
                Destroy();

                // The flock's GameObject stays at the origin, so the quads are in world space.
                m_Object = new GameObject("Murmurations_Flock");
                m_Object.transform.SetParent(parent, false);

                m_Renderer = m_Object.AddComponent<MeshRenderer>();
                m_Renderer.shadowCastingMode = ShadowCastingMode.Off;
                m_Renderer.receiveShadows = false;
                m_Renderer.allowOcclusionWhenDynamic = false;
            }

            if (m_Mesh == null)
            {
                m_Mesh = new Mesh();
                m_Mesh.name = "Murmurations_Flock";
                m_Mesh.MarkDynamic();

                MeshFilter meshFilter = m_Object.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = m_Object.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = m_Mesh;
            }

            int frameCount = materials.Length;
            int quadCount = capacity * frameCount;
            int vertexCount = quadCount * 4;

            m_Vertices = new Il2CppStructArray<Vector3>(vertexCount);
            Il2CppStructArray<Vector2> uv = new(vertexCount);
            Il2CppStructArray<Color32> colors = new(vertexCount);
            Vector3 hidden = hidePosition.ToUnity();
            Color32 white = default;
            white.r = white.g = white.b = white.a = 255;

            for (int quad = 0; quad < quadCount; quad++)
            {
                int vertex = quad * 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    m_Vertices[vertex + corner] = hidden;
                    colors[vertex + corner] = white;

                    Vector2 cornerUv = default;
                    cornerUv.x = corner & 1;
                    cornerUv.y = corner >> 1;
                    uv[vertex + corner] = cornerUv;
                }
            }

            m_Mesh.Clear();
            m_Mesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m_Mesh.vertices = m_Vertices;
            m_Mesh.uv = uv;
            m_Mesh.colors32 = colors;
            m_Mesh.subMeshCount = frameCount;

            for (int frame = 0; frame < frameCount; frame++)
            {
                // Same corner order and winding as Unity's built-in quad.
                Il2CppStructArray<int> triangles = new(capacity * 6);
                for (int bird = 0; bird < capacity; bird++)
                {
                    int vertex = (frame * capacity + bird) * 4;
                    int index = bird * 6;
                    triangles[index] = vertex;
                    triangles[index + 1] = vertex + 3;
                    triangles[index + 2] = vertex + 1;
                    triangles[index + 3] = vertex + 3;
                    triangles[index + 4] = vertex;
                    triangles[index + 5] = vertex + 2;
                }

                m_Mesh.SetTriangles(triangles, frame);
            }

            m_Renderer.sharedMaterials = new Il2CppReferenceArray<Material>(materials);
            m_Mesh.RecalculateBounds();

            m_Materials = materials;
            m_Capacity = capacity;
            m_FrameCount = frameCount;
            m_ShownBirdCount = 0;
        }

        private void HideBird(int index, Vec3 position)
        {
            for (int f = 0; f < m_FrameCount; f++) CollapseQuad((f * m_Capacity + index) * 4, position);
        }

        private void CollapseQuad(int vertex, Vec3 position)
        {
            if (m_Vertices == null) return;

            Vector3 point = position.ToUnity();
            m_Vertices[vertex] = point;
            m_Vertices[vertex + 1] = point;
            m_Vertices[vertex + 2] = point;
            m_Vertices[vertex + 3] = point;
        }
    }
}
