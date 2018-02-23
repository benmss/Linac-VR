using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System;


using UnityEngine;

public class ModelLoader {

  string filePath;
  string fname;

  int[,,] pixels;
  int[,,] voxels;
  int x, y, z;
  int meshRangeOverride;
  int meshRangeMin;
  int meshRangeMax;

  bool drawMesh;

  float zMod = 0.2f;

  Vector3 slicePos;

  MarchingMeshCreator meshMarcher = new MarchingMeshCreator();


  public ModelLoader(string filePath, string fname, int meshRangeOverride, int meshRangeMin, int meshRangeMax, bool drawMesh) {
    this.filePath = filePath;
    this.fname = fname;
    this.meshRangeOverride = meshRangeOverride;
    this.meshRangeMin = meshRangeMin;
    this.meshRangeMax = meshRangeMax;
    this.drawMesh = drawMesh;
  }

  public void LoadModel() {
    FileInfo[] files = FileReader.GetFilesFromFolder(filePath);
    files = FileReader.GetSortedImages(files);

    //Determine size of pixel array, x and y from 2D image slice, z from number of slices
    int[] dim = FileReader.GetDimensions(files[0]);
    x = dim[0]; y = dim[1];
    foreach (FileInfo f in files) {
      if (f.Name.StartsWith("CT")) {
        z++;
      }
    }
    print("Loading Model: " + fname + " | " + x + "," + y + "," + z + " | " + dim[3] + "/" + dim[2]);
    pixels = new int[dim[0],dim[1],z];
    // pixelList = new List<List<Vector2>>();
    // pHalf = (int)Math.Pow(2,dim[2]-1);
    // pFull = (int)Math.Pow(2,dim[2]);

    // if (dim[3] == 1) { signed = true; }
    // pMin = (signed ? -pHalf : 0);
    // pMax = (signed ? -pMin - 1 : pFull);
    voxels = new int[x,y,z];

    //If the pixel bit format within the Dicom file is not 16 bit,
    //problems might arise, i.e. it is untested.
    if (dim[2] != 16) {
      UnityEngine.Debug.LogError("Pixel Bits: " + dim[2]);
      return;
    }

    //Read file data, images and RT_Structure
    int count = 0;
    List<FileInfo> rtFiles = new List<FileInfo>();
    foreach (FileInfo f in files) {
      if (f.Name.StartsWith("RT")) {
        rtFiles.Add(f);
        continue;
      }
      // print("Reading file: " + f.Name);
      string s = FileReader.GetPixels(f, ref pixels, x, y, count);
      if (s != "") {
        string[] split = s.Split('\\');
        float vx = Mathf.Round(float.Parse(split[0]));
        float vy = Mathf.Round(float.Parse(split[1]));
        float vz = Mathf.Round(float.Parse(split[2]));
        slicePos = new Vector3(vx,vy,vz);
      }
      count++;
    }

    foreach (FileInfo f in rtFiles) {
      if (f.Name.Contains("Structure")) {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        LoadStructureSet(f,fname);
        sw.Stop();
        FileReader.printStopwatch(sw,"Thread: ");
      } else if (f.Name.Contains("Plan")) {
        // LoadPlan(f);
      } else if (f.Name.Contains("Dose")) {
        // LoadDose(f);
      }
    }

  }

  /** ======================================================

    Reads a Dicom RT Structure file and produces a 3D mesh
    from the data within. Structure files contain several
    different structures and each of these is turned into
    a separate mesh.

  ======================================================= **/
  void LoadStructureSet(FileInfo file, string fname) {
    Stopwatch sw = new Stopwatch();
    Stopwatch sw2 = new Stopwatch();
    Stopwatch swCPU = new Stopwatch();
    Stopwatch swMarch = new Stopwatch();
    FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
    List<string> objectNames = new List<string>();
    int block = 0;

    List<Color> colours = new List<Color>();
    List<List<float>> ranges = new List<List<float>>();

    List<MeshMaker.ModelData> modelData = new List<MeshMaker.ModelData>();

    List<List<List<Vector3>>> zData = new List<List<List<Vector3>>>();
    List<List<Vector3>> sData = new List<List<Vector3>>();
    float zCounter = 0;
    float zLast = float.MinValue;
    bool zNew = true;
    bool singlePoint = false;
    Vector3 point = Vector3.zero;

    float xMin = float.MaxValue; float xMax = float.MinValue;
    float yMin = float.MaxValue; float yMax = float.MinValue;
    float zMin = float.MaxValue; float zMax = float.MinValue;
    int models = 0;

    sw.Start();
    sw2.Start();
    while (fs.Position < fs.Length && fs.Position >= 0) {
      string tag = FileReader.GetNextTag(fs);
      int length;

      if (tag == FileReader.S_1 || tag == FileReader.S_2 || tag == FileReader.S_3 || tag == FileReader.S_4) {
        if (block == 0) { block = 1; }
        fs.Position += FileReader.S_GAP;
        continue;
      }

      if (tag == FileReader.S_BLOCK_TAG) {
        fs.Position += FileReader.S_GAP - 4;
        continue;
      }

      if (tag == FileReader.S_BLOCK_TAG_2) {
        fs.Position += FileReader.S_GAP_2 - 4;
        continue;
      }

      if (tag == FileReader.S_5) {
        if (block == 1) {
          block = 2;
          continue;
        }
      }

      if (tag == FileReader.S_6) {
        if (block == 2) {
          block = 3;
          continue;
        }
      }

      //A new structure entry denoted by a colour tag (2nd object onwards)
      //Mesh for previous structure is created here, and related vars reset.
      if (block == 31 && tag == FileReader.S_COL) {
        block = 3;
        List<float> values = new List<float>();
        values.Add(xMin); values.Add(xMax);
        values.Add(yMin); values.Add(yMax);
        values.Add(zMin); values.Add(zMax);
        ranges.Add(values);
        xMin = float.MaxValue; xMax = float.MinValue;
        yMin = float.MaxValue; yMax = float.MinValue;
        zMin = float.MaxValue; zMax = float.MinValue;


        //Create mesh for current model and reset vars for the next
        if ((meshRangeOverride != -1 && models == meshRangeOverride) ||
        (meshRangeOverride == -1 && models >= meshRangeMin && models <= meshRangeMax)) {
          if (drawMesh) {
            if (!singlePoint) {
              // print("Model: " + model.model);
              // print("Range: " + ranges.Count);
              // print("Colours: " + colours.Count);              
              modelData.Add(MeshMaker.CreateModelData(zData, ranges[ranges.Count-1], meshMarcher, models,
                  colours[colours.Count-1], swMarch, ""));
            } else {
              modelData.Add(MeshMaker.CreateModelData(point, ""));
            }
          }
        }
        zData = new List<List<List<Vector3>>>();
        sData = new List<List<Vector3>>();
        zNew = true;
        zCounter = 0;
        singlePoint = false;

        models++;
      }

      //Read colour of structure
      if (block == 3 && tag == FileReader.S_COL) {
        length = FileReader.GetNextLength(fs);
        byte[] bytes = new byte[length];
        fs.Read(bytes,0,length);
        string s = "";
        foreach (byte b in bytes) {
          s += (char)b;
        }
        string[] ss = s.Split('\\');

        Color c = new Color(float.Parse(ss[0])/255,float.Parse(ss[1])/255,float.Parse(ss[2])/255);
        colours.Add(c);
        continue;
      }

      //Preparation for structure contour data (other tag removal)
      if (tag == FileReader.S_7) {
        if (block == 3) {
          block = 31;
          continue;
        }
      }

      //Read structure contour data
      if (block == 31 && tag == FileReader.S_DATA) {
        length = FileReader.GetNextLength(fs);
        byte[] bytes = new byte[length];

        //Read and add points to list
        fs.Read(bytes,0,length);
        List<Vector3> points = new List<Vector3>();
        float[] p = new float[3];
        int pCounter = 0;
        string current = "";
        foreach (byte b in bytes) {
          char c = (char)b;
          if (c == '\\') {
            p[pCounter] = float.Parse(current);
            pCounter++;
            current = "";
            if (pCounter == 3) {
              pCounter = 0;
              points.Add(new Vector3(p[0],p[1],p[2]));
              p = new float[3];
            }
          } else {
            current += c;
          }
        }
        //Add last point
        p[pCounter] = float.Parse(current);
        points.Add(new Vector3(p[0],p[1],p[2]));

        float vz = points[0].z * zMod;
        if (vz < zMin) { zMin = vz; }
        if (vz > zMax) { zMax = vz; }

        //Points are added to lists based on slice and structure data
        //There can be multiple structures per slice (i.e. neck and arms)
        if (!zNew && vz != zLast) {
          zData.Add(sData);
          sData = new List<List<Vector3>>();
          sData.Add(points);

          zCounter++;
          zLast = vz;
        }

        if (points.Count == 1) {
          print(models + " Single Point: " + points[0]);
          singlePoint = true;
          point = points[0];
        }

        if (zNew) {
          zNew = false;
        }

        for (int i = 0; i < points.Count; i++) {
          Vector3 v = points[i];
          if (v.x < xMin) { xMin = v.x; }
          if (v.x > xMax) { xMax = v.x; }
          if (v.y < yMin) { yMin = v.y; }
          if (v.y > yMax) { yMax = v.y; }
        }

        continue;
      }

      string nextTag = FileReader.GetNextTag(fs);
      fs.Position -= 4;
      if (nextTag == FileReader.S_BLOCK_TAG) {
        fs.Position += FileReader.S_GAP;
        continue;
      }

      if (nextTag == FileReader.S_BLOCK_TAG_2) {
        fs.Position += FileReader.S_GAP_2;
        continue;
      }


      // print("Tag: " + tag);
      if (tag == FileReader.S_NAME) {
        length = FileReader.GetNextLength(fs);
        objectNames.Add(FileReader.GetData(fs,length));
        continue;
      }

      //Normal Tag
      length = FileReader.GetNextLength(fs);
      fs.Position += length;
    }

    for (int i = 0; i < objectNames.Count; i++) {
      // print(objectNames[i]);
    }


    //Could redesign this to remove duplicate code, duplicate is under "if (block == 31 && tag == S_COL)"
    List<float> values2 = new List<float>();
    values2.Add(xMin); values2.Add(xMax);
    values2.Add(yMin); values2.Add(yMax);
    values2.Add(zMin); values2.Add(zMax);
    ranges.Add(values2);

    models++;
    //Create mesh for final model
    if ((meshRangeOverride != -1 && models == meshRangeOverride) ||
    (meshRangeOverride == -1 && models >= meshRangeMin && models <= meshRangeMax)) {
      if (drawMesh) {
        if (!singlePoint) {
          modelData.Add(MeshMaker.CreateModelData(zData, ranges[ranges.Count-1], meshMarcher, models,
          colours[colours.Count-1], swMarch, ""));
        } else {
          modelData.Add(MeshMaker.CreateModelData(point, ""));
        }        
      }
    }
    
    for (int i = 0; i < modelData.Count; i++) {
      modelData[i].name = objectNames[i];
    }

    sw.Stop();
    sw2.Start();
    for (int i = 0; i < modelData.Count; i++) {
      if (i == 0) {
        modelData[i] = MeshMaker.MakeMesh(modelData[i], pixels, slicePos);
        modelData[i].topName = fname;
      } else {
        modelData[i] = MeshMaker.MakeMesh(modelData[i], null, Vector3.zero);
      }
    }
    sw2.Stop();
    // FileReader.printStopwatch(sw,"Model Loader - Non Marching: ");
    // FileReader.printStopwatch(sw2,"Model Loader - Marching: ");

    //Close file
    fs.Close();

    sw.Reset();
    sw.Start();
    //Try to notify main thread
    while (true) {
      if (FileReader.modelData != null) {
        Thread.Sleep(100);
        continue;
      }
      FileReader.modelData = modelData;
      break;
    }
    sw.Stop();
    // FileReader.printStopwatch(sw,"Model Loader - Notify Wait: ");
  }

  void print(string s) {
    UnityEngine.Debug.Log(s);
  }
}
