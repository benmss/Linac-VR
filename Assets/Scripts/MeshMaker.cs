using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Diagnostics;

using MarchingCubesProject;
using MarchingCubesGPUProject;

public class MeshMaker {

  public struct ModelData {
    public List<List<List<Vector3>>> data;
    public List<float> dimensions;
    public MarchingMeshCreator meshMarcher;
    public int model;
    public Color colour;
    public Stopwatch sw;
    public List<MarchingMeshCreator.MeshData> meshData;
    public byte[,,] sliceData;
    public string name;
  }

  public struct Model {
    public string name;
    public List<GameObject> models;
    public List<Material> mats;
    public List<List<float>> dimensions;
    public byte[,,] sliceData;
    public int zMin;
  }

  private static Vector3[] adjacent = {new Vector3(-1,0,0),new Vector3(1,0,0),new Vector3(0,-1,0),new Vector3(0,1,0)};
  public const int upper = 2;
  public const int lower = 2;

  /** ==================================================================================================

    Creates a 3D mesh from the passed pixel data.
    This main steps of this process:
     1. Use Bresenham's 2D line algorithm to plug gaps in each slice.
     2. For each structure, find a point that has a neighbour that is inside the structure
     3. Perform flood fill to fill in structure
     4. Run Marching Cubes on filled slices to produce 3D mesh

    Step 3 seems to have a reasonable chance of failure (~30% as a guess), and fills the wrong part
    Winding number and cross number algorithms from a reputable source seem to produce incorrect value
    E.g.
       xxxxxxxxx
       x       x
       x       x
       x   xxxxx
       xxxxx@

    The point being checked "@" in the above image, says it is within the polygon
    Issue is fixed by checking whether the pixel at 0,0,0 has been filled, as a structure
    will never be there due to padding. If 0,0,0 is filled, current filled area is inverted

    Due to point in a polygon methods being unreliable (and giving different answers usually),
    it might be better to remove the check and just pick a pixel at random, inverting result as
    needed. Depending on ratio of structure pixels to void pixels, it might even be faster.

    Parameters:
    data        - The pixel data stored by slice number (Z pos), then structure number
                  (multiple per slice is possible).
    dimensions  - Contains lowest and highest values for x, y and z dimensions.
    meshMarcher - GameObject that has a MarchingMeshCreator component to perform Marching Cubes algorithm.
    models      - An integer representing the current model number.
    color       - Colour of current object read from the file.
    noFill      - Not used, but can be set to draw mesh using primitive cubes instead of Marching Cubes.
    swM         - Stopwatch for tracking time spent running Marching Cubes.

    Generated 3D meshes are added as objects under the MeshMarcher, with a parent representing the
    current model.

  ================================================================================================== **/
  public static ModelData MakeMesh(ModelData model, int[,,] slicePixels, Vector3 slicePosition) {
    int xMin = (int)Mathf.Round(model.dimensions[0]-lower);
    int xMax = (int)Mathf.Round(model.dimensions[1]+upper);
    int yMin = (int)Mathf.Round(model.dimensions[2]-lower);
    int yMax = (int)Mathf.Round(model.dimensions[3]+upper);
    int zMin = (int)Mathf.Round(model.dimensions[4]-lower);
    int zMax = (int)Mathf.Round(model.dimensions[5]+upper);

    int width = xMax - xMin;
    int height = yMax - yMin;
    int depth = zMax - zMin;

    bool printOnce = false;

    byte[] sliceData = null;
    byte[,,] sliceData2 = null;
    float[] pixels = new float[width * height * depth];
    int xd = 0, yd = 0, zd = 0;
    if (model.model == 0) {
      sliceData = new byte[width * height * depth];
      int w2 = slicePixels.GetLength(0);
      int h2 = slicePixels.GetLength(1);
      int d2 = slicePixels.GetLength(2);
      sliceData2 = new byte[w2,h2,d2];
      xd = (int)Mathf.Round(model.dimensions[0] - slicePosition.x);
      yd = (int)Mathf.Round(model.dimensions[2] - slicePosition.y);
      zd = (int)Mathf.Round(model.dimensions[4] - slicePosition.z);
    }

    // Material mat = new Material(Shader.Find("Standard"));
    // mat.color = c;

    //For each 2D slice (|Z| value)
    
    for (int z = 0; z < model.data.Count; z++) {
      //For each |S|tructure within a single 2D slice
      for (int s = 0; s < model.data[z].Count; s++) {
        //Apply Bresenham's 2D line algorithm and create Set of resulting points
        HashSet<Vector3> voxelPoints = new HashSet<Vector3>();

        //For each |P|oint in this structure
        for (int p = 0; p < model.data[z][s].Count; p++) {
          Vector3 v1 = model.data[z][s][p];
          Vector3 v2;
          if (p < model.data[z][s].Count - 1) {
            v2 = model.data[z][s][p+1];
          } else {
            v2 = model.data[z][s][0];
          }

          //Bresenham's Algorithm
          List<Vector3> vox = VoxelLine(v1,v2,z);
          foreach (Vector3 v in vox) {
            voxelPoints.Add(v);
          }
        }

        //Find original point that has a neighbour within bounds of structure and fill volume from it
        //Loop terminates when a valid point is found
        for (int p = 0; p < model.data[z][s].Count; p++) {
          Vector3 point = new Vector3(Mathf.Round(model.data[z][s][p].x),Mathf.Round(model.data[z][s][p].y),z);
          Vector3 adjPoint = Vector3.zero;
          bool found = false;

          for (int i = 0; i < adjacent.Length && !found; i++) {
            adjPoint = point + adjacent[i];

            //Check not part of structure
            if (voxelPoints.Contains(adjPoint)) { continue; }

            //Check inside structure
            if (!InsideStructure(adjPoint, model.data[z][s])) { continue; }

            //Found point
            found = true;
          }

          if (!found) { continue; }

          //Flood fill from found point
          List<Vector3> listPoints = FloodFill2(adjPoint, voxelPoints, model.dimensions);

          //Convert points to 1D array for marching cubes
          foreach (Vector3 v in listPoints) {
            int idx = ((int)Mathf.Round(v.x - xMin)) +
                      ((int)Mathf.Round(v.y - yMin)) * width +
                      ((int)Mathf.Round(v.z)) * width * height;
            pixels[idx] = 1;
            if (model.model == 0) {
              int ix = (int)Mathf.Round(v.x - xMin + xd);
              int iy = (int)Mathf.Round(v.y - yMin + yd);
              // int iz = (int)Mathf.Round(v.z - zMin - zd);
              int iz = (int)Mathf.Round(v.z)-1;
              sliceData2[ix,iy,iz] = 1;
              // sliceData[idx] = 1;
              if (!printOnce) {
                UnityEngine.Debug.Log(v + " -> (" + ix + "," + iy + "," + iz + ") | " +
                xd + "," + yd + "," + zd + ", zRange: " + model.data.Count);
                printOnce = true;
              }
            }
          }
          if (model.model == 0) {
            model.data[z][s] = listPoints;
          }

          //Terminate loop
          break;
        }
      }
    }

    //Slices
    model.data = null;
    if (model.model == 0) {








      model.sliceData = sliceData2;
    }

    model.sw.Start();
    model.meshData = model.meshMarcher.CreateMesh(pixels,width,height,depth,model.model);
    // UnityEngine.Debug.Log("MeshData: " + model.meshData.Count);
    // MarchingMeshCreator m = meshMarcher.GetComponent<MarchingMeshCreator>();
    // m.CreateMesh(pixels,width,height,depth,model.model);

    model.sw.Stop();
    return model;
  }

  /** ======================================================

    Move models created by FileReader.LoadStructureSet,
    from a single structure file, into the correct places
    relative to each other.

  ======================================================= **/
  public static void FixPositions(List<List<float>> ranges, GameObject meshMarcher) {
    Transform t0 = meshMarcher.transform.GetChild(0);
    Vector3 center = Vector3.zero;
    for (int i = 0; i < ranges.Count; i++) {
      List<float> dimensions = ranges[i];

      Transform t = meshMarcher.transform.GetChild(i);
      if (t.childCount == 0) { continue; }

      int xMin = (int)Mathf.Round(dimensions[0]-lower);
      int xMax = (int)Mathf.Round(dimensions[1]+upper);
      int yMin = (int)Mathf.Round(dimensions[2]-lower);
      int yMax = (int)Mathf.Round(dimensions[3]+upper);
      int zMin = (int)Mathf.Round(dimensions[4]-lower);
      int zMax = (int)Mathf.Round(dimensions[5]+upper);

      int width = xMax - xMin;
      int height = yMax - yMin;
      int depth = zMax - zMin;

      if (i == 0) {
        Vector3 half = new Vector3(width*.5f,height*.5f,depth*.5f);
        center = new Vector3(xMin + half.x, yMin + half.y, zMin + half.z);
      } else {
        Vector3 target = new Vector3(xMin,yMin,zMin);
        Vector3 half = new Vector3(width*.5f,height*.5f,depth*.5f);
        target += half;
        target -= center;
        t.transform.position = target;
      }
    }

    for (int i = 1; i < meshMarcher.transform.childCount;) {
      meshMarcher.transform.GetChild(i).parent = t0;
    }
  }



  public static Model CreateModel(List<ModelData> modelData, string name) {
    Model model = new Model();
    GameObject top = new GameObject(name);

    int meshCounter = 0;

    List<GameObject> gos = new List<GameObject>();
    List<Material> mats = new List<Material>();
    List<List<float>> dims = new List<List<float>>();
    // List<HashSet<Vector3>> sliceData = new List<HashSet<Vector3>>();

    for (int i = 0; i < modelData.Count; i++) {
      ModelData md = modelData[i];
      if (md.meshData.Count == 0) { continue; }
      GameObject mid = new GameObject ("Model " + i);
      mid.transform.parent = top.transform;
      Material mat = new Material(Shader.Find("Standard"));
      mat.color = md.colour;

      gos.Add(mid);
      mats.Add(mat);
      dims.Add(md.dimensions);


      //Create mesh gameobject
      // UnityEngine.Debug.Log("Model " + i + ", " + md.meshData.Count);
      foreach (MarchingMeshCreator.MeshData med in md.meshData) {
        Mesh mesh = new Mesh();
        mesh.SetVertices(med.verts);
        mesh.SetTriangles(med.indices, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        GameObject go = new GameObject("Mesh " + meshCounter);
        go.transform.parent = mid.transform;
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.GetComponent<Renderer>().material = mat;
        go.GetComponent<MeshFilter>().mesh = mesh;
        meshCounter++;
      }

      AlignMeshes(mid.transform);
    }

    model.sliceData = modelData[0].sliceData;
    ModelData md0 = modelData[0];
    md0.sliceData = null;
    modelData[0] = md0;

    model.name = name;
    model.models = gos;
    model.mats = mats;
    model.dimensions = dims;

    return model;
  }

  public static void AlignMeshes(Transform mid) {
    //Center objects

    // Calculate bounds of whole object
    float xMin = float.MaxValue; float xMax = float.MinValue;
    float yMin = float.MaxValue; float yMax = float.MinValue;
    float zMin = float.MaxValue; float zMax = float.MinValue;

    for (int i = 0; i < mid.transform.childCount; i++) {
      mid.transform.localScale = new Vector3(1,1,5);
      MeshFilter mf = mid.transform.GetChild(i).GetComponent<MeshFilter>();
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

    // Calculate new center
    Vector3 center = new Vector3((xMin+xMax)*.5f,(yMin+yMax)*.5f,(zMin+zMax)*.5f);
    // UnityEngine.Debug.Log("New Center: " + center);

    // Update mesh positions
    for (int i = 0; i < mid.transform.childCount; i++) {
      mid.transform.GetChild(i).localPosition = new Vector3(-center.x,-center.y,-center.z);
    }
  }


  public static ModelData CreateModelData(List<List<List<Vector3>>> data, List<float> dimensions, MarchingMeshCreator meshMarcher, int model, Color colour, Stopwatch sw, string name) {
    ModelData m = new ModelData();
    m.data = data;
    m.dimensions = dimensions;
    m.meshMarcher = meshMarcher;
    m.model = model;
    m.colour = colour;
    m.sw = sw;
    m.name = name;
    return m;
  }

  public static void ScaleModel(GameObject meshMarcher, Vector3 pos, Vector3 dim) {
    Transform t0 = meshMarcher.transform.GetChild(0);

  }


  static bool InsideStructure(Vector3 point, List<Vector3> structure) {
    if (structure[0] != structure[structure.Count-1]) { UnityEngine.Debug.LogError("V mismatch"); }
    // return InPoly(point,structure) != 0;
    return InPoly2(point,structure) != 0;
  }

  //Winding Number implementation (IsLeft & InPoly, and InPoly2) by Dan Sunday:
  //geomalgorithms.com/a03-_inclusion.html
  static int IsLeft(Vector3 p0, Vector3 p1, Vector3 p2) {
    return (int)((p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y));
  }

  static int InPoly(Vector3 p, List<Vector3> v) {
    int wn = 0;
    for (int i = 0; i < v.Count-1; i++) {
      // Vector3 v2 = (i == v.Count - 1 ? v[0] : v[i+1]);
      Vector3 v2 = v[i+1];
      v2 = new Vector3(Mathf.Round(v2.x),Mathf.Round(v2.y),v2.z);
      if (v[i].y <= p.y && v2.y > p.y && IsLeft(v[i],v2,p) > 0) {
        wn++;
      } else if (v[i].y > p.y && v2.y <= p.y && IsLeft(v[i],v2,p) < 0) {
        wn--;
      }
    }
    return wn;
  }

  //Crossing Number test also by Dan Sunday
  static int InPoly2(Vector3 p, List<Vector3> v) {
    int cn = 0;
    for (int i = 0; i < v.Count; i++) {
      Vector3 v2 = (i == v.Count - 1 ? v[0] : v[i+1]);
      v2 = new Vector3(Mathf.Round(v2.x),Mathf.Round(v2.y),v2.z);
      if (((v[i].y <= p.y) && (v2.y > p.y)) ||
      ((v[i].y > p.y) && (v2.y <= p.y))) {
        float vt = (float)(p.y - v[i].y) / (v2.y - v[i].y);
        if (p.x < v[i].x + vt * (v2.x - v[i].x)) {
          cn++;
        }
      }
    }
    return (cn&1);
  }

  //Slightly better flood fill method
  //Uses a byte array to represent pixels, faster to read and set at the cost of increased mem usage
  static List<Vector3> FloodFill2(Vector3 start, HashSet<Vector3> points, List<float> dimensions) {
    List<Vector3> p = new List<Vector3>();
    LinkedList<Vector3> targetPoints = new LinkedList<Vector3>();
    targetPoints.AddLast(start);

    int xMin = (int)Mathf.Round(dimensions[0]-lower);
    int xMax = (int)Mathf.Round(dimensions[1]+upper);
    int yMin = (int)Mathf.Round(dimensions[2]-lower);
    int yMax = (int)Mathf.Round(dimensions[3]+upper);
    int zMin = (int)Mathf.Round(dimensions[4]-lower);
    int zMax = (int)Mathf.Round(dimensions[5]+upper);
    int width = xMax - xMin;
    int height = yMax - yMin;
    int depth = zMax - zMin;

    byte[,] pixels = new byte[width,height];

    //Flag boundary pixels as 3
    foreach (Vector3 v in points) {
      pixels[(int)v.x - xMin,(int)v.y - yMin] = 3;
    }

    //Loop through pixels starting from the 'start' pixel
    //Pixels to be visited are flagged with value 1
    //Pixels that have been visited are flagged with value 2
    //This prevents pixels from being looked at twice
    while (targetPoints.Count != 0) {
      Vector3 v = targetPoints.First.Value;
      int x = (int)v.x - xMin;
      int y = (int)v.y - yMin;
      pixels[x,y] = 2;

      for (int i = 0; i < adjacent.Length; i++) {
        int x2 = (int)(v.x + adjacent[i].x - xMin);
        int y2 = (int)(v.y + adjacent[i].y - yMin);
        if (x2 < 0 || x2 >= width) { continue; }
        if (y2 < 0 || y2 >= height) { continue; }
        if (pixels[x2,y2] != 0) { continue; }
        pixels[x2,y2] = 1;
        targetPoints.AddLast(v + adjacent[i]);
      }
      targetPoints.RemoveFirst();
    }

    if (pixels[0,0] == 2) {
      //FloodFill has filled the wrong area, invert array to fix
      //Boundary pixels are set to 3 so they are ignored
      for (int i = 0 ; i < width; i++) {
        for (int j = 0; j < height; j++) {
          if (pixels[i,j] == 2) {
            pixels[i,j] = 0;
          } else if (pixels[i,j] == 0) {
            pixels[i,j] = 2;
          }
        }
      }
    }

    //Convert byte array into list of pixels
    for (int i = 0 ; i < width; i++) {
      for (int j = 0; j < height; j++) {
        if (pixels[i,j] >= 2) {
          p.Add(new Vector3(i+xMin,j+yMin,start.z));
        }
      }
    }
    return p;
  }

  //Bresenham's 2D line algorithm, taken from Wikipedia
  static List<Vector3> VoxelLine(Vector3 a, Vector3 b, int z) {
    //For simplicity round points to nearest integer
    Vector3 v0 = new Vector3(Mathf.Round(a.x), Mathf.Round(a.y), z);
    Vector3 v1 = new Vector3(Mathf.Round(b.x), Mathf.Round(b.y), z);

    //Octant check
    if (Math.Abs(v1.y - v0.y) < Math.Abs(v1.x - v0.x)) {
      //Low
      if (v0.x > v1.x) {
        return VoxelLineLow(v1, v0, z);
      } else {
        return VoxelLineLow(v0, v1, z);
      }
    } else {
      //High
      if (v0.y > v1.y) {
        return VoxelLineHigh(v1, v0, z);
      } else {
        return VoxelLineHigh(v0, v1, z);
      }
    }
  }

  static List<Vector3> VoxelLineLow(Vector3 v0, Vector3 v1, int z) {
    List<Vector3> points = new List<Vector3>();
    float dx = v1.x - v0.x;
    float dy = v1.y - v0.y;
    int i = 1;
    if (dy < 0) {
      i = -1;
      dy = -dy;
    }
    float D = 2*dy - dx;
    int y = (int)v0.y;

    for (int x = (int)v0.x; x <= v1.x; x++) {
      points.Add(new Vector3(x,y,z));
      if (D > 0) {
        y = y + i;
        D = D - 2*dx;
      }
      D = D + 2*dy;
    }

    return points;
  }

  static List<Vector3> VoxelLineHigh(Vector3 v0, Vector3 v1, int z) {
   List<Vector3> points = new List<Vector3>();
    float dx = v1.x - v0.x;
    float dy = v1.y - v0.y;
    int i = 1;
    if (dx < 0) {
      i = -1;
      dx = -dx;
    }
    float D = 2*dx - dy;
    int x = (int)v0.x;

    for (int y = (int)v0.y; y <= v1.y; y++) {
      points.Add(new Vector3(x,y,z));
      if (D > 0) {
        x = x + i;
        D = D - 2*dy;
      }
      D = D + 2*dx;
    }

    return points;
  }

}
