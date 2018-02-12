using UnityEngine;
using System.Collections.Generic;

// using ProceduralNoiseProject;

namespace MarchingCubesProject {

    public enum MARCHING_MODE {  CUBES, TETRAHEDRON };

    public class MarchingMeshCreator : MonoBehaviour {

        public Material m_material;

        public MARCHING_MODE mode = MARCHING_MODE.CUBES;

        public int seed = 0;
        
        public struct MeshData {
          public List<Vector3> verts;
          public List<int> indices;
        }

        // public float isoSurface = 0.0f;

        List<GameObject> meshes = new List<GameObject>();

        public List<MeshData> CreateMesh(float[] voxels, int width, int height, int depth, int models) {
            //Set the mode used to create the mesh.
            //Cubes is faster and creates less verts, tetrahedrons is slower and creates more verts but better represents the mesh surface.
            Marching marching = null;
            if(mode == MARCHING_MODE.TETRAHEDRON)
                marching = new MarchingTertrahedron();
            else
                marching = new MarchingCubes();

            //Surface is the value that represents the surface of mesh
            //For example the perlin noise has a range of -1 to 1 so the mid point is where we want the surface to cut through.
            //The target value does not have to be the mid point it can be any value with in the range.
            marching.Surface = 0.0f;

            List<Vector3> verts = new List<Vector3>();
            List<int> indices = new List<int>();

            //The mesh produced is not optimal. There is one vert for each index.
            //Would need to weld vertices for better quality mesh.
            marching.Generate(voxels, width, height, depth, verts, indices);

            //A mesh in unity can only be made up of 65000 verts.
            //Need to split the verts between multiple meshes.

            int maxVertsPerMesh = 64998; //must be divisible by 3, ie 3 verts == 1 triangle
            int numMeshes = verts.Count / maxVertsPerMesh + 1;
            // GameObject top = new GameObject("Model " + models);
            // top.transform.parent = transform;
            
            List<MeshData> meshes = new List<MeshData>();
           
            for (int i = 0; i < numMeshes; i++) {
                List<Vector3> splitVerts = new List<Vector3>();
                List<int> splitIndices = new List<int>();

                for (int j = 0; j < maxVertsPerMesh; j++) {
                    int idx = i * maxVertsPerMesh + j;
                    
                    if (idx < verts.Count) {
                        if (verts[idx].x == 0) {
                          splitVerts.Add(new Vector3(-10,verts[idx].y, verts[idx].z));
                        } else {
                          splitVerts.Add(verts[idx]);
                        }
                        splitIndices.Add(j);
                    }
                    
                }

                if (splitVerts.Count == 0) continue;

                MeshData m = new MeshData();
                m.verts = splitVerts;
                m.indices = splitIndices;
                meshes.Add(m);
                
                // Mesh mesh = new Mesh();
                // mesh.SetVertices(splitVerts);
                // mesh.SetTriangles(splitIndices, 0);
                // mesh.RecalculateBounds();
                // mesh.RecalculateNormals();

                // GameObject go = new GameObject("Mesh");
                // go.transform.parent = top.transform;
                // go.AddComponent<MeshFilter>();
                // go.AddComponent<MeshRenderer>();
                // go.GetComponent<Renderer>().material = m_material;
                // go.GetComponent<MeshFilter>().mesh = mesh;
                // go.transform.localPosition = new Vector3(-width / 2, -height / 2, -depth / 2);

                // meshes.Add(go);
            }
            return meshes;
            
            //Center objects
            
          //Calculate bounds of whole object
          /* float xMin = float.MaxValue; float xMax = float.MinValue;
          float yMin = float.MaxValue; float yMax = float.MinValue;
          float zMin = float.MaxValue; float zMax = float.MinValue;
          
          for (int i = 0; i < top.transform.childCount; i++) {
            top.transform.localScale = new Vector3(1,1,5);
            MeshFilter mf = top.transform.GetChild(i).GetComponent<MeshFilter>();
            if (!mf) { continue; }
            
            Bounds b = mf.mesh.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            
            if (c.x - e.x < xMin) { xMin = c.x - e.x; }
            if (c.x + e.x > xMax) { xMax = c.x + e.x; }
            if (c.y - e.y < yMin) { yMin = c.y - e.y; }
            if (c.y + e.y > yMax) { yMax = c.y + e.y; }
            if (c.z - e.z < zMin) { zMin = c.z - e.z; }
            if (c.z + e.z > zMax) { zMax = c.z + e.z; }
            // UnityEngine.Debug.Log("Center: " + c);
            // UnityEngine.Debug.Log("Extent: " + e);
          }
          
          //Calculate new center
          Vector3 center = new Vector3((xMin+xMax)*.5f,(yMin+yMax)*.5f,(zMin+zMax)*.5f);
          // UnityEngine.Debug.Log("New Center: " + center);
          
          //Update mesh positions
          for (int i = 0; i < top.transform.childCount; i++) {              
            top.transform.GetChild(i).localPosition = new Vector3(-center.x,-center.y,-center.z);
          } */
            
        }
    }

}