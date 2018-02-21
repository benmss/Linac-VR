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

  List<MeshData> meshData;
  int threadCount = 0;

  List<GameObject> meshes = new List<GameObject>();

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

    //#TODO change from 6 to 2
    int[,] dimensions = new int[nThreads,6];
    for (int i = 0; i < nThreads; i++) {
      if (i == 0) {
        //Min
        dimensions[i,0] = 0;
        dimensions[i,2] = 0;
        dimensions[i,4] = 0;

        //Max
        dimensions[i,1] = width / nThreads;
        dimensions[i,3] = height / nThreads;
        dimensions[i,5] = depth / nThreads;
      } else {
        //Min
        dimensions[i,0] = dimensions[i-1,1] + 1;
        dimensions[i,2] = dimensions[i-1,3] + 1;
        dimensions[i,4] = dimensions[i-1,5] + 1;

        //Max
        if (i != (nThreads-1)) {
          dimensions[i,1] = dimensions[0,1] * (i+1);
          dimensions[i,3] = dimensions[0,3] * (i+1);
          dimensions[i,5] = dimensions[0,5] * (i+1);
        } else {
          dimensions[i,1] = width-2;
          dimensions[i,3] = height-2;
          dimensions[i,5] = depth-2;
        }
      }
    }

    // print("Dims: " + width + "," + height + "," + depth);
    // for (int i = 0; i < nThreads; i++) {
      // print(dimensions[i,0] + "," + dimensions[i,1]);
      // print(dimensions[i,2] + "," + dimensions[i,3]);
      // print(dimensions[i,4] + "," + dimensions[i,5]);
      // print("=============");
    // }

    // meshData = new MeshData[nThreads];
    meshData = new List<MeshData>();
    threadCount = 0;

    if (nThreads > 1) {
      //Multi thread
      // Thread t0 = Thread.CurrentThread;
      Thread t1, t2, t3 = null, t4 = null;

      List<Vector3> v1 = new List<Vector3>();
      List<int> i1 = new List<int>();
      MeshData md = new MeshData();
      md.verts = v1;
      md.indices = i1;
      meshData.Add(md);
      t1 = new Thread(() => {
        // meshData[0] = mc2.Generate(voxels, width, height, depth, v1, i1, dimensions, 0);
        mc2.Generate(voxels, width, height, depth, ref v1, ref i1, dimensions, 0);
        // Interlocked.Increment(ref threadCount);
        // t0.Interrupt();
      });
      t1.Start();

      List<Vector3> v2 = new List<Vector3>();
      List<int> i2 = new List<int>();
      md = new MeshData();
      md.verts = v2;
      md.indices = i2;
      meshData.Add(md);
      t2 = new Thread(() => {
        mc3.Generate(voxels, width, height, depth, ref v2, ref i2, dimensions, 1);
        // Interlocked.Increment(ref threadCount);
        // t0.Interrupt();
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
          mc4.Generate(voxels, width, height, depth, ref v3, ref i3, dimensions, 2);
          // Interlocked.Increment(ref threadCount);
          // t0.Interrupt();
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
          mc5.Generate(voxels, width, height, depth, ref v4, ref i4, dimensions, 3);
          // Interlocked.Increment(ref threadCount);
          // t0.Interrupt();
        });
        t4.Start();
      }

      //Wait for all threads to finish
      // while (threadCount < nThreads) {
        // try {
          // Thread.Sleep(50);
          // if (threadCount >= nThreads) { break; }
          // Thread.Sleep(Timeout.Infinite);
        // } catch (ThreadInterruptedException e) {
          // continue;
        // }
      // }

      t1.Join();
      t2.Join();
      if (t3 != null) { t3.Join(); }
      if (t4 != null) { t4.Join(); }
    } else {
      //Single thread
      mc2.Generate(voxels, width, height, depth, ref verts, ref indices, new int[1,6]{{0,width-2,0,height-1,0,depth-1}},0);
      MeshData md = new MeshData();
      md.verts = verts;
      md.indices = indices;
      meshData.Add(md);
    }

    // print("Threads: " + nThreads);
    // foreach (MeshData md in meshData) {
      // print("MeshData v/i: " + md.verts.Count + "," + md.indices.Count);
    // }




    // mc2.Generate(voxels, width, height, depth, verts, indices, new int[]{0}, 0);

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

      if (meshDataCounter >= meshData[meshDataNumber].verts.Count) {
        meshDataNumber++;
        meshDataCounter = 0;
        if (meshDataNumber >= nThreads) {
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
}

