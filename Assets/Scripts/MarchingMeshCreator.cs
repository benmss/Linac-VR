using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using System;


public class MarchingMeshCreator : MonoBehaviour {

  public int seed = 0;

  public class MeshData {
    public List<Vector3> verts;
    public List<int> indices;
  }

  // float sizeThreshold = 250000;

  List<MeshData>[] meshDataLists;
  List<MeshData> meshData;

  int threadCount = 0;

  List<GameObject> meshes = new List<GameObject>();
  List<Thread> threads = new List<Thread>();


  public List<MeshData> CreateMesh(float[] voxels, int width, int height, int depth, int models) {
    MarchingCubes2 mc2 = new MarchingCubes2();
    MarchingCubes2 mc3 = new MarchingCubes2();
    MarchingCubes2 mc4 = new MarchingCubes2();
    MarchingCubes2 mc5 = new MarchingCubes2();

    List<Vector3> verts = new List<Vector3>();
    List<int> indices = new List<int>();

    //Split load over multiple threads
    float size = width * height * depth;

    //Performance Testing
    //======================================================================================
    //On my PC which has 4 cores and no hyperthreading (Core i5-2500k 3.3GHz)
    //With only 3 tests each (Almost no background processes, Lung Data Set):
    //1 Thread  - Average 19s 760ms  100%
    //2 Threads - Average 14s 600ms   74%
    //3 Threads - Average 12s 956ms   66%
    //4 Threads - Average 12s 116ms   61%

    //Also tried a mixture, where smaller objects used less threads, but it was always slower.
    //Code here could be optimised more, and switching to a thread pool might help.

    //From tests above, 4 threads looks best.
    //However, 3 might be safer, freeing up some CPU time for Unity/VR Kit/Other Stuff.
    //Assuming a similar CPU is being used.
    //========================================================================================
    int nThreads = 4;


    float xMod = width / nThreads;
    int[] xD = new int[nThreads+1];
    for (int i = 0; i < xD.Length; i++) {
      xD[i] = (int)Mathf.Round(i * xMod);
    }
    xD[xD.Length-1] = width-2;

    int[][] dimensions = new int[nThreads][];
    for (int i = 0; i < nThreads; i++) {
      dimensions[i] = new int[2];
      dimensions[i][0] = xD[i];
      dimensions[i][1] = xD[i+1];
      if (i != 0) {
        dimensions[i][0]++;
      }
    }


    // meshData = new MeshData[nThreads];
    meshData = new List<MeshData>();
    threadCount = 0;

    if (nThreads > 1) {
      Thread t1, t2, t3 = null, t4 = null;

      List<Vector3> v1 = new List<Vector3>();
      List<int> i1 = new List<int>();
      MeshData md = new MeshData();
      md.verts = v1;
      md.indices = i1;
      meshData.Add(md);
      t1 = new Thread(() => {
        mc2.Generate(voxels, width, height, depth,  v1,  i1, dimensions[0], 0);
      });
      t1.Start();

      List<Vector3> v2 = new List<Vector3>();
      List<int> i2 = new List<int>();
      md = new MeshData();
      md.verts = v2;
      md.indices = i2;
      meshData.Add(md);
      t2 = new Thread(() => {
        mc3.Generate(voxels, width, height, depth,  v2,  i2, dimensions[1], 1);
      });
      t2.Start();

      if (nThreads >= 3) {
        List<Vector3> v3 = new List<Vector3>();
        List<int> i3 = new List<int>();
        md = new MeshData();
        md.verts = v3;
        md.indices = i3;
        meshData.Add(md);
        t3 = new Thread(() => {
          mc4.Generate(voxels, width, height, depth,  v3,  i3, dimensions[2], 2);
        });
        t3.Start();
      }

      if (nThreads >= 4) {
        List<Vector3> v4 = new List<Vector3>();
        List<int> i4 = new List<int>();
        md = new MeshData();
        md.verts = v4;
        md.indices = i4;
        meshData.Add(md);
        t4 = new Thread(() => {
          mc5.Generate(voxels, width, height, depth,  v4,  i4, dimensions[3], 3);
        });
        t4.Start();
      }



      t1.Join();
      t2.Join();
      if (t3 != null) { t3.Join(); }
      if (t4 != null) { t4.Join(); }
    } else {
      //Single thread
      mc2.Generate(voxels, width, height, depth,  verts,  indices, dimensions[0],0);
      MeshData md = new MeshData();
      md.verts = verts;
      md.indices = indices;
      meshData.Add(md);
    }

    //** Actual limit is 65534 (Unity is lying)
    //A mesh in unity can only be made up of 65000 verts.
    //Need to split the verts between multiple meshes.
    int maxVertsPerMesh = 65532; //must be divisible by 3, ie 3 verts == 1 triangle
    int numMeshes = verts.Count / maxVertsPerMesh + 1;

    List<MeshData> meshes = new List<MeshData>();

    int totalIndexCount = 0;
    int totalIndexMax = 0;
    int totalVertCount = 0;
    for (int m = 0; m < nThreads; m++) {
      if (meshData[m].verts != null) {
        totalIndexMax += meshData[m].verts.Count;
      } else {
        print("MeshData " + m + " verts is null");
      }
    }
    if (totalIndexMax == 0) {
      return null;
    }



    int meshCount = 0;
    verts = new List<Vector3>();
    indices = new List<int>();
    Dictionary<Vector3,int> vertDict = new Dictionary<Vector3,int>();
    int indexCount = 0;
    int lastIndex = 0;
    int meshDataNumber = 0;
    int meshDataCounter = 0;
    bool done = false;
    while (true) {
      int idx = meshDataCounter;

      if (meshData[meshDataNumber].verts.Count != 0) {
        for (int i = 0; i < 3; i++) {
          if (vertDict.ContainsKey(meshData[meshDataNumber].verts[idx+i])) {
            indices.Add(vertDict[meshData[meshDataNumber].verts[idx+i]]);
          } else {
            verts.Add(meshData[meshDataNumber].verts[idx+i]);
            totalVertCount++;
            indices.Add(lastIndex);
            vertDict.Add(meshData[meshDataNumber].verts[idx+i],lastIndex);
            lastIndex++;
          }
        }

        if (done) { break; }

        indexCount+= 3;
        meshDataCounter += 3;
        totalIndexCount += 3;

        bool next = false;

        if (indexCount >= maxVertsPerMesh) {
          MeshData m = new MeshData();
          m.verts = verts;
          m.indices = indices;
          meshes.Add(m);
          verts = new List<Vector3>();
          indices = new List<int>();
          vertDict = new Dictionary<Vector3,int>();

          meshCount++;
          lastIndex = 0;
          indexCount = 0;
        }
      }

      if (meshDataCounter >= meshData[meshDataNumber].verts.Count) {
        meshDataNumber++;
        bool doneCheck = false;
        if (meshDataNumber >= meshData.Count) {
          doneCheck = true;
        } else {
          while (meshDataNumber < meshData.Count && meshDataNumber < nThreads && meshData[meshDataNumber].verts.Count == 0) {
            meshDataNumber++;
          }
        }

        meshDataCounter = 0;
        if (doneCheck || meshDataNumber >= nThreads) {
          //Done
          MeshData m = new MeshData();
          m.verts = verts;
          m.indices = indices;
          meshes.Add(m);
          break;
        }
      }
    }

    return meshes;
  }


  //Mesh Splitter version
  public List<MeshData> CreateMeshWithSplitting(float[] voxels, int width, int height, int depth, int models) {
    MarchingCubes2 mc2 = new MarchingCubes2();
    MarchingCubes2 mc3 = new MarchingCubes2();
    MarchingCubes2 mc4 = new MarchingCubes2();
    MarchingCubes2 mc5 = new MarchingCubes2();

    List<Vector3> verts = new List<Vector3>();
    List<int> indices = new List<int>();

    meshDataLists = new List<MeshData>[4];

    //Split load over multiple threads
    float size = width * height * depth;

    int nThreads = 4;

    int width2 = width / 2;
    int depth2 = depth / 2;
    //Split cube across 4 threads like so: (top down view)
    // 0 1
    // 2 3
    //
    // 0: 0 - width2, 0 - depth2
    // 1: width2+1 - width, 0 - depth2
    // 2: 0 - width2, depth2+1 - depth
    // 3: width2+1 - width, depth2+1 - depth
    int[][] dimensions = new int[nThreads][];
    for (int i = 0; i < 4; i++) {
      dimensions[i] = new int[4];
      if (i % 2 == 0) {
        dimensions[i][0] = 0;
        dimensions[i][1] = width2;
      } else {
        dimensions[i][0] = width2;
        dimensions[i][1] = width;
      }
      if (i < 2) {
        dimensions[i][2] = 0;
        dimensions[i][3] = depth2;
      } else {
        dimensions[i][2] = depth2;
        dimensions[i][3] = depth;
      }
    }

    threadCount = 0;
    int splitNumber = 4;

    if (nThreads > 1) {
      Thread t1, t2, t3, t4;

      List<Vector3> v1 = new List<Vector3>();
      List<int> i1 = new List<int>();
      t1 = new Thread(() => {
        try {
          meshDataLists[0] = mc2.Generate(voxels, width, height, depth, v1, i1, dimensions[0], 0, splitNumber);
        } catch (Exception e) {
          UnityEngine.Debug.Log(e);
        }
      });
      threads.Add(t1);
      t1.Start();

      List<Vector3> v2 = new List<Vector3>();
      List<int> i2 = new List<int>();
      t2 = new Thread(() => {
        try {
          meshDataLists[1] = mc3.Generate(voxels, width, height, depth,  v2,  i2, dimensions[1], 1, splitNumber);
        } catch (Exception e) {
          UnityEngine.Debug.Log(e);
        }
      });
      threads.Add(t2);
      t2.Start();

      List<Vector3> v3 = new List<Vector3>();
      List<int> i3 = new List<int>();
      t3 = new Thread(() => {
        try {
          meshDataLists[2] = mc4.Generate(voxels, width, height, depth,  v3,  i3, dimensions[2], 2, splitNumber);
        } catch (Exception e) {
          UnityEngine.Debug.Log(e);
        }
      });
      threads.Add(t3);
      t3.Start();

      List<Vector3> v4 = new List<Vector3>();
      List<int> i4 = new List<int>();
      t4 = new Thread(() => {
        try {
          meshDataLists[3] = mc5.Generate(voxels, width, height, depth,  v4,  i4, dimensions[3], 3, splitNumber);
        } catch (Exception e) {
          UnityEngine.Debug.Log(e);
        }
      });
      threads.Add(t4);
      t4.Start();


      t1.Join();
      t2.Join();
      t3.Join();
      t4.Join();
    }

    List<MeshData> meshData = new List<MeshData>();
    for (int i = 0; i < meshDataLists.Length; i++) {
      if (meshDataLists[i] != null) {
        for (int j = 0; j < meshDataLists[i].Count; j++) {
          meshData.Add(meshDataLists[i][j]);
        }
      }
    }
    return meshData;
  }

  void OnDestroy() {
    foreach (Thread t in threads) {
      if (t.IsAlive) {
        t.Abort();
      }
    }
  }
}

