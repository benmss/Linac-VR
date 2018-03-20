
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System;


using UnityEngine;

public class ModelLoader {

  FileInfo file;
  string fname;

  int[,,] pixels;
  int[,,] voxels;
  int x, y, z;
  int meshRangeOverride;
  int meshRangeMin;
  int meshRangeMax;

  bool drawMesh;

  // float zMod = 0.2f;
  // float zMod = 1/3f;

  Vector3 slicePos;

  MarchingMeshCreator meshMarcher = new MarchingMeshCreator();


  public ModelLoader(FileInfo file, string fname, int meshRangeOverride, int meshRangeMin, int meshRangeMax, bool drawMesh) {
    this.file = file;
    this.fname = fname;
    this.meshRangeOverride = meshRangeOverride;
    this.meshRangeMin = meshRangeMin;
    this.meshRangeMax = meshRangeMax;
    this.drawMesh = drawMesh;
  }

  public void LoadModel() {
    LoadStructureSet(file,fname);
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

    HashSet<string> sequenceBlocks = FileReader.GetSequenceBlocks();
    Dictionary<int,string> nameList = new Dictionary<int,string>();
    List<int> structNumbers = new List<int>();
    int currentNumber = 0;

    bool zDistFound = false;
    float zMod = 1;
    float zH1 = 0;
    float zH2 = 0;
    float zState = 0;


    sw.Start();
    sw2.Start();

    //General process of reading the DICOM files:
    //0. Some DICOM files seem to contain unknown header information in a non tag format
    //   To skip past this data, we search for a signpost to normality. In other words
    //   one of the following tags: "0008,0005" "0008,0016" "0008,0018"
    //   If none of these can be found we bail after some number of characters, as
    //   we have no hope of accomplishing anything in the unknown location we are in.

    //1. Skip all non relevant tags
    //   These are of the type - Tag 4 bytes, Length 4 bytes, Data Length bytes
    //
    //2. Some tags denote sequences or encapsulated data
    //   There are two main types - Tag 4 bytes, Length 4 bytes, <Data>
    //                            - Tag 4 bytes, Length 4 bytes, <Data>, Tag 4 bytes, Length 4 bytes
    //   These tags have to be checked carefully as <Data> represents a series of type 1. tags
    //   Method used here is to skip reading the data portion when a sequence tag is found
    //   Checked against FileReader.S_1, S_2, etc.
    //
    //3. The data we actually we want is found using the colour tag: "3006,002A"
    //   A colour tag is followed by a data tag: "3006,0050" and the pixel data it contains
    //
    //4. Once the first colour tag is found, each subsequent colour tag becomes a new structure object.
    //
    //5. Lastly, the file contains a list of names that match the structure objects previously found.
    //   The tag for these is "3006,0085". The list is in the same order as the structures.
    int bailLimit = 500;
    bool started = false;
    while (fs.Position < fs.Length && fs.Position >= 0) {
      string tag = FileReader.GetNextTag(fs);
      int length;

      if (!started) {
        if (tag == FileReader.S_SP_1 || tag == FileReader.S_SP_2 || tag == FileReader.S_SP_3) {
          started = true;
          length = FileReader.GetNextLength(fs);
          fs.Position += length;
          continue;
        } else {
          if (fs.Position >= bailLimit) {
            //Failed to load this file
            Logger.Log("Failed to load model from file: " + file.FullName);
            break;
          }
          if (tag != "0000,0000") {
            // string tag0 = FileReader.GetNextTag(fs);
            // fs.Position -= 4;
            // print("Tag: " + tag + " " + tag0);
          }
          fs.Position -= 3;
          bailLimit += 1;
          continue;
        }
      }

      if (tag == FileReader.S_ID_0) {
        fs.Position += 4;
        length = 2;
        currentNumber = FileReader.GetDataIntString(fs,length);
        continue;
      }

      if (tag == FileReader.S_NAME_0) {
        length = FileReader.GetNextLength(fs);
        string s = FileReader.GetData(fs,length);
        nameList.Add(currentNumber,s);
        // print("Added Number/Name: " + currentNumber + ", " + s);
        continue;
      }

      if (tag == FileReader.S_DATA_ID) {
        fs.Position += 4;
        length = 2;
        int n = FileReader.GetDataIntString(fs,2);
        if (structNumbers.Count <= models) {
          structNumbers.Add(n);
          // print("Added Struct Number: " + structNumbers[structNumbers.Count-1]);
        }
        continue;
      }

      if (sequenceBlocks.Contains(tag)) {
        fs.Position += 4;
        continue;
      }

      if (tag == "FFFF,FFFF") {
        //Blank tag
        continue;
      }

      //A new structure entry denoted by a colour tag (2nd object onwards)
      //Mesh for previous structure is created here, and related vars reset.
      if (block == 12 && tag == FileReader.S_COL) {
        block = 0;
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
      if ((block == 0 || block == 11) && tag == FileReader.S_COL) {
        length = FileReader.GetNextLength(fs);
        byte[] bytes = new byte[length];
        fs.Read(bytes,0,length);
        string s = "";
        foreach (byte b in bytes) {
          s += (char)b;
        }
        // print("Colour found: " + s + " | " + (fs.Position-(8+length)));
        string[] ss = s.Split('\\');
        Color c = new Color(float.Parse(ss[0])/255,float.Parse(ss[1])/255,float.Parse(ss[2])/255);
        if (block == 11) {
          // colours[colours.Count-1] = c;
        } else {
          block = 11;
          colours.Add(c);
          // print("Colour added: " + s);
        }
        continue;

      }


      //Read structure contour data
      if ((block == 11 || block == 12) && tag == FileReader.S_DATA) {
        if (block == 11) { block = 12; }
        length = FileReader.GetNextLength(fs);
        // print("Data found: " + length + " | " + (fs.Position-(8)));
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
          if (zState == 0) {
            zH1 = vz;
            zState = 1;
          } else if (zState == 1) {
            zH2 = vz;
            zState = 2;
          }
        }

        if (points.Count == 1) {
          // print(models + " Single Point: " + points[0]);
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


      //Normal Tag
      length = FileReader.GetNextLength(fs);
      fs.Position += length;
    }



    for (int i = 0; i < objectNames.Count; i++) {
      print(objectNames[i]);
    }


    //Could redesign this to remove duplicate code, duplicate is under "if (block == 31 && tag == S_COL)"
    List<float> values2 = new List<float>();
    values2.Add(xMin); values2.Add(xMax);
    values2.Add(yMin); values2.Add(yMax);
    values2.Add(zMin); values2.Add(zMax);
    ranges.Add(values2);

    models++;
    print(file.Name + " read: " + colours.Count + " colours, " + models + " models");
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


    bool isoFound = false;
    int isoIdx = 0;
    for (int i = 0; i < modelData.Count; i++) {
      string s = "Unknown " + i;
      if (nameList.ContainsKey(structNumbers[i])) {
        s = nameList[structNumbers[i]];
      }
      modelData[i].name = s;
      string nameLower = s.ToLower();
      if (modelData[i].point && !isoFound) {
        if (nameLower.Contains("isocenter") || nameLower.Contains("izocenter") ||
              nameLower.Contains("iso center") || nameLower.Contains("izo center") ||
              nameLower.Contains("isocentre") || nameLower.Contains("izocentre") ||
              nameLower.Contains("iso centre") || nameLower.Contains("izo centre")) {
          isoIdx = i;
          isoFound = true;
        }
      }
    }
    
    //Apply z scaling
    int zScale = (int)Mathf.Round(Mathf.Abs(zH2 - zH1));
    float zScaleF = 1/(float)zScale;
    MeshMaker.zScale = zScale;
    print("zScale: " + zScale + ", " + zScaleF + " zH1: " + zH1);
    
    
    //An alternative to using the expensive collider method of finding mesh surface points for markers
    //This method is much faster but needs more work to function correctly.
    //Currently the found points do not seem to match the raycast/collider version at all, nor do they seem
    //be within the mesh
    //Comparing to the rotated and scaled mesh might be an issue
    //#TODO
    //Issues:
    //The position of the cubes that are in line with the isocenter in the original data do not match
    //up with the points found by raycasting the final mesh.
    
    /* if (isoFound) {
      Vector3 isoPoint = modelData[isoIdx].pointPosition;
      print("IsoPoint: " + modelData[isoIdx].pointPosition + ", md0.z: " + modelData[0].data[40].Count);      
      int zCount = modelData[0].data.Count;
      int isoZ = (int)(isoPoint.z);
      float pxMin = isoPoint.x;
      float pyMin = isoPoint.y;
      bool zFound = false;
      int zIdx = 0;
      for (int i = 0; i < modelData[0].data.Count && !zFound; i++) {
        int sCount = modelData[0].data[i].Count;
        if (sCount > 0) {
          for (int j = 0; j < sCount && !zFound; j++) {
            if (modelData[0].data[i][j].Count > 0) {
              for (int k = 0; k < modelData[0].data[i][j].Count; k++) {
                if (Mathf.Round(modelData[0].data[i][j][k].z) == isoPoint.z) {
                  zIdx = i;
                  zFound = true;
                  break;
                }
              }
            }
          }
        }
      }
      
      GameObject ttp = new GameObject("Cubes");
      
      int zIdx2 = zIdx;
      for (zIdx = 0; zIdx < modelData[0].data.Count; zIdx++) {
        
        int zv = -9000;
        pxMin = isoPoint.x;
        pyMin = isoPoint.y;
        float pxMax = isoPoint.x;
        float pyMax = isoPoint.y;
        HashSet<Vector3> zPoints = new HashSet<Vector3>();
        for (int s = 0; s < modelData[0].data[zIdx].Count; s++) {
            for (int p = 0; p < modelData[0].data[zIdx][s].Count; p++) {            
            Vector3 v1 = modelData[0].data[zIdx][s][p];
            if (zv == -9000) { zv = (int)Mathf.Round(v1.z); }
            Vector3 v2;            
            if (p < modelData[0].data[zIdx][s].Count - 1) {
              v2 = modelData[0].data[zIdx][s][p+1];
            } else {
              v2 = modelData[0].data[zIdx][s][0];
            }

            //Bresenham's Algorithm
            List<Vector3> vox = MeshMaker.VoxelLine(v1,v2,zv);
            foreach (Vector3 v in vox) {
              zPoints.Add(v);
            }
          }
        }
      
        foreach (Vector3 pv in zPoints) {
          
          //Decrease y from -40 to find bCube point
          //Decrease x from -100 to find cCube point
          //Iso: -100, -40, 40
          //b: -100, -70, 40
          //c: -138, -40, 40
          
          if (zIdx == zIdx2) {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = pv;
            cube.name = pv.ToString();
            cube.transform.parent = ttp.transform;
          }
          
          // if (modelData[0].data[i][j][k] == isoPoint) { bv = true; }
          // float px = Mathf.Round(pv.x);
          // float py = Mathf.Round(pv.y);
          // if (Mathf.Abs(px - isoPoint.x) < 2) {
          // if (px == isoPoint.x) {
            // print("x - ijk: " + i + ", " + j + ", " + k + " | " + pv);
          // if (zIdx == zIdx2) {
          if (pv.x <= isoPoint.x && pv.y == isoPoint.y) {
            pxMin = Mathf.Min(pxMin,pv.x);
          }
          if (pv.x >= isoPoint.x && pv.y == isoPoint.y) {
            pxMax = Mathf.Max(pxMax,pv.x);
          }
            // }
            // if (Mathf.Abs(py - isoPoint.y) < 2) {
            // if (py < isoPoint.y + 2 || py > isoPoint.y - 2) {
            // if (py == isoPoint.y || py == isoPoint.y + 1 ||) {
              // print("y - ijk: " + i + ", " + j + ", " + k + " | " + pv);
          if (pv.y <= isoPoint.y && pv.x == isoPoint.x) {
            pyMin = Mathf.Min(pyMin,pv.y);
          }
          if (pv.y >= isoPoint.y && pv.x == isoPoint.x) {
            pyMax = Mathf.Max(pyMax,pv.y);
          }
          // }
          
        }
      
                
        // if (zIdx == zIdx2) {
          print("zIdx " + zIdx + ", z: " + zv + ", pxMin: " + pxMin + ", pyMin: " + pyMin + ", pxMax: " + pxMax + ", pyMax: " + pyMax + ", width: " + (pxMax - pxMin) + ", height: " + (pyMax - pyMin));
          // UnityEngine.Debug.DrawLine(new Vector3(pxMin, isoPoint.y, isoPoint.z), isoPoint, Color.cyan, 20);
          // UnityEngine.Debug.DrawLine(new Vector3(isoPoint.x, pyMin, isoPoint.z), isoPoint, Color.magenta, 20);
        // }
      }
      // UnityEngine.Debug.LogError("Iso Test");
      // print("ModelData z: " + modelData[0].data.Count);
      // for (int i = 0; i < modelData[0].data.Count; i++) {
        // print("z: " + modelData[0].data[i][0][0].z);
      // }
      // return;
    } */
    
    
    


    


    sw.Stop();
    sw2.Start();
    for (int i = 0; i < modelData.Count; i++) {
      if (modelData[i].dimensions != null) {
        modelData[i].dimensions[4] *= zScaleF;
        modelData[i].dimensions[5] *= zScaleF;
      }
      if (i == 0) {
        modelData[i] = MeshMaker.MakeMesh(modelData[i], pixels, slicePos);
        modelData[i].topName = fname;
        modelData[i].zMin = zH1 * zScale;
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
