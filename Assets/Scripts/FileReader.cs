using UnityEngine;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System;
using System.Diagnostics;

using MarchingCubesProject;



public class FileReader : MonoBehaviour {

  //2^1, 2^8, 2^16, 2^24
  int[] hexMod = {1,16*16,256*256,4096*4096};

  int pMin; int pMax;
  int pHalf; int pFull;
  int x; int y; int z;
  bool signed = false;
  int ax = 0; int co = 0; int sa = 0;

  int sliceTimer = 0;
  int sliceTimerMax = 0;
  int sliceTimerTick = 2;
  Dictionary<int, int[]> sliceRange = new Dictionary<int, int[]>();

  int[,,] pixels;
  List<List<Vector2>> pixelList;
  int[,,] voxels;
  const float zMod = 0.2f;
  Transform rotate = null;

  int[] playCounter = new int[3];
  float[] playTick = new float[3];
  float[] positionalStep = new float[3];
  float[] positionalStart = new float[3];

  [Header("Slice Viewer")]
  public GameObject transverse;
  public GameObject coronal;
  public GameObject sagittal;
  public bool transversePlay;
  public bool coronalPlay;
  public bool sagittalPlay;

  [Header("Rendering")]
  public GameObject meshMarcher;
  public GameObject testBox;
  public bool drawMesh;
  [Range(0,20)]
  public int meshRangeMin = 0;
  [Range(0,20)]
  public int meshRangeMax = 1;
  [Range(-1,20)]
  public int meshRangeOverride = 0;

  MarchingMeshCreator meshMarcherC;
  Dictionary<string,MeshMaker.Model> models = new Dictionary<string,MeshMaker.Model>();
  Dictionary<string,int[,,]> slices = new Dictionary<string,int[,,]>();
  Dictionary<string,Vector3> slicePosition = new Dictionary<string,Vector3>();
  public static List<MeshMaker.ModelData> modelData;

  string ModelNameGenerator(string filePath) {
    string fname;
    string[] split = filePath.Split('/');
    if (split == null) {
      fname = filePath;
    } else {
      fname = split[split.Length-1];
    }

    if (models.ContainsKey(name)) {
      int count = 1;
      string name2 = name;
      while (models.ContainsKey(name + " #" + count)) {
        count++;
      }
      name += " #" + count;
    }

    return name;
  }

	// Use this for initialization
	void Start () {
    string filePath = "../../LUNG DICOM DATA";
    string fname = ModelNameGenerator(filePath);
    FileInfo[] files = GetFilesFromFolder(filePath);
    // FileInfo[] files = GetFilesFromFolder("../../PELVIS DICOM DATA");
    files = GetSortedImages(files);

    if (meshMarcher) {
      meshMarcherC = meshMarcher.GetComponent<MarchingMeshCreator>();
    }

    //Determine size of pixel array
    int[] dim = GetDimensions(files[0]);
    int z = 0;
    foreach (FileInfo f in files) {
      if (f.Name.StartsWith("CT")) {
        z++;
      }
    }
    print("Dimensions: " + dim[0] + "," + dim[1] + "," + z);
    print("Pixel Rep: " + dim[3] + ", Bits: " + dim[2]);
    pixels = new int[dim[0],dim[1],z];
    pixelList = new List<List<Vector2>>();
    pHalf = (int)Math.Pow(2,dim[2]-1);
    pFull = (int)Math.Pow(2,dim[2]);

    this.x = dim[0];
    this.y = dim[1];
    this.z = z;
    if (dim[3] == 1) { signed = true; }
    pMin = (signed ? -pHalf : 0);
    pMax = (signed ? -pMin - 1 : pFull);
    voxels = new int[x,y,z];

    if (dim[2] != 16) {
      UnityEngine.Debug.LogError("Pixel Bits: " + dim[2]);
      return;
    }

    int count = 0;
    List<FileInfo> rtFiles = new List<FileInfo>();
    foreach (FileInfo f in files) {
      if (f.Name.StartsWith("RT")) {
        // LoadRTFile(f,fname);
        rtFiles.Add(f);
        continue;
      }
      // print("Reading file: " + f.Name);
      string s = GetPixels(f, ref pixels, count);
      if (s != "") {
        string[] split = s.Split('\\');
        float vx = Mathf.Round(float.Parse(split[0]));
        float vy = Mathf.Round(float.Parse(split[1]));
        float vz = Mathf.Round(float.Parse(split[2]));
        slicePosition.Add(fname,new Vector3(vx,vy,vz));
      }
      count++;
    }
    slices.Add(fname,pixels);

    foreach (FileInfo f in rtFiles) {
      LoadRTFile(f,fname);
    }


    positionalStep[0] = transverse.transform.localScale.x / (x-1.0f);
    positionalStep[1] = transverse.transform.localScale.x / (y-1.0f);
    positionalStep[2] = transverse.transform.localScale.x / (z-1.0f);

    positionalStart[0] = transverse.transform.position.x - transverse.transform.localScale.x * 0.5f;
    positionalStart[1] = coronal.transform.position.y - coronal.transform.localScale.y * 0.5f;
    positionalStart[2] = sagittal.transform.position.z - sagittal.transform.localScale.x * 0.5f;

    sliceTimerMax = (int)Math.Max(Math.Max(x,y),z) * sliceTimerTick;
    playTick[0] = sliceTimerMax / z;
    playTick[1] = sliceTimerMax / y;
    playTick[2] = sliceTimerMax / x;

    ShowSlice(0,0);
    ShowSlice(1,0);
    ShowSlice(2,0);
	}

  void LoadRTFile(FileInfo f, string fname) {
    if (f.Name.Contains("Structure")) {

      //Thread test
      var th = new Thread(() => {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        LoadStructureSet(f,fname);
        sw.Stop();
        printStopwatch(sw,"Thread: ");
      });
      th.Start();



    } else if (f.Name.Contains("Plan")) {
      LoadPlan(f);
    } else if (f.Name.Contains("Dose")) {
      LoadDose(f);
    }
  }

  /** ======================================================

    Reads a Dicom RT Structure file and produces a 3D mesh
    from the data within. Structure files contain several
    different structures and each of these is turned into
    a separate mesh.

  ======================================================= **/
  void LoadStructureSet(FileInfo file, string name) {
    Stopwatch sw = new Stopwatch();
    Stopwatch sw2 = new Stopwatch();
    Stopwatch swCPU = new Stopwatch();
    Stopwatch swMarch = new Stopwatch();
    FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
    int block = 0;

    List<Color> colours = new List<Color>();
    List<List<float>> ranges = new List<List<float>>();

    List<MeshMaker.ModelData> modelData = new List<MeshMaker.ModelData>();

    List<List<List<Vector3>>> zData = new List<List<List<Vector3>>>();
    List<List<Vector3>> sData = new List<List<Vector3>>();
    float zCounter = 0;
    float zLast = float.MinValue;
    bool zNew = true;

    float xMin = float.MaxValue; float xMax = float.MinValue;
    float yMin = float.MaxValue; float yMax = float.MinValue;
    float zMin = float.MaxValue; float zMax = float.MinValue;
    int models = 0;

    sw.Start();
    sw2.Start();
    while (fs.Position < fs.Length && fs.Position >= 0) {
      string tag = GetNextTag(fs);
      int length;

      if (tag == S_1 || tag == S_2 || tag == S_3 || tag == S_4) {
        if (block == 0) { block = 1; }
        fs.Position += S_GAP;
        continue;
      }

      if (tag == S_BLOCK_TAG) {
        fs.Position += S_GAP - 4;
        continue;
      }

      if (tag == S_BLOCK_TAG_2) {
        fs.Position += S_GAP_2 - 4;
        continue;
      }

      if (tag == S_5) {
        if (block == 1) {
          block = 2;
          continue;
        }
      }

      if (tag == S_6) {
        if (block == 2) {
          block = 3;
          continue;
        }
      }

      //A new structure entry denoted by a colour tag (2nd object onwards)
      //Mesh for previous structure is created here, and related vars reset.
      if (block == 31 && tag == S_COL) {
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
            modelData.Add(MeshMaker.CreateModelData(zData,ranges[ranges.Count-1],meshMarcherC,models,colours[colours.Count-1],swMarch,name));
          }
        }
        zData = new List<List<List<Vector3>>>();
        sData = new List<List<Vector3>>();
        zNew = true;
        zCounter = 0;

        models++;
      }

      //Read colour of structure
      if (block == 3 && tag == S_COL) {
        length = GetNextLength(fs);
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
      if (tag == S_7) {
        if (block == 3) {
          block = 31;
          continue;
        }
      }

      //Read structure contour data
      if (block == 31 && tag == S_DATA) {
        length = GetNextLength(fs);
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
        p[pCounter] = float.Parse(current);
        points.Add(new Vector3(p[0],p[1],p[2]));

        float vz = points[0].z * zMod;
        if (vz < zMin) { zMin = vz; }
        if (vz > zMax) { zMax = vz; }

        //Points are added lists to based on slice and structure data
        //There can be multiple structures per slice (i.e. neck and arms)
        if (!zNew && vz != zLast) {
          zData.Add(sData);
          sData = new List<List<Vector3>>();
          sData.Add(points);

          zCounter++;
          zLast = vz;
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
      length = GetNextLength(fs);
      fs.Position += length;
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
        modelData.Add(MeshMaker.CreateModelData(zData,ranges[ranges.Count-1],meshMarcherC,models,colours[colours.Count-1],swMarch,name));
      }
    }

    for (int i = 0; i < modelData.Count; i++) {
      if (i == 0) {
        modelData[i] = MeshMaker.MakeMesh(modelData[i], slices[name], slicePosition[name]);
      } else {
        modelData[i] = MeshMaker.MakeMesh(modelData[i], null, Vector3.zero);
      }
    }

    //Close file
    fs.Close();

    //Try to notify main thread
    while (true) {
      if (FileReader.modelData != null) {
        Thread.Sleep(100);
        continue;
      }
      FileReader.modelData = modelData;
      break;
    }





    //Print out time taken
    // sw.Stop();
    // sw2.Stop();
    // printStopwatch(sw, "Structure Time: ");
    // if (drawMesh) { printStopwatch(swCPU, "Total Mesh Time (CPU): "); }
    // if (drawMesh) { printStopwatch(swMarch, "Marching Time: "); }
    // printStopwatch(sw2, "Total Time: ");
    // print("Done.");

    // if (drawMesh) { MeshMaker.FixPositions(ranges, meshMarcher); }

    //Rotating models before they have been rendered once doesn't work, so flag call on next update.
    //Could use a coroutine instead.
    // rotate = true;
  }


  public static void printStopwatch(Stopwatch sw, string text) {
    TimeSpan ts = sw.Elapsed;
    string es = String.Format("{0:00}m {1:00}s {2:00}ms", ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
    UnityEngine.Debug.Log(text + es);
  }

  void LoadPlan(FileInfo f) {
    //#TODO
  }

  void LoadDose(FileInfo f) {
    //#TODO
  }


	// Update is called once per frame
	void Update () {
    if (rotate != null) {
      rotate.Rotate(0,0,180);
      rotate = null;
    }

    if (FileReader.modelData != null) {
      Stopwatch sw = new Stopwatch();
      sw.Start();
      List<MeshMaker.ModelData> mdl = FileReader.modelData;
      FileReader.modelData = null;
      MeshMaker.Model m = MeshMaker.CreateModel(mdl,mdl[0].name);
      models.Add(m.name,m);
      MeshMaker.FixPositions(models[name].dimensions,models[name].models[0].transform.parent.gameObject);
      rotate = models[name].models[0].transform.parent;
      // print("Slice");
      ShowSlice(m.name,0,70);
      sw.Stop();
      printStopwatch(sw, "Update Model: ");
    }

    if (sliceTimer >= sliceTimerMax) {
      sliceTimer = 0;
      playCounter[0] = 0;
      playCounter[1] = 0;
      playCounter[2] = 0;
      return;
    }

    if (transversePlay && transverse.activeInHierarchy) {
      if (sliceTimer >= playCounter[0] * playTick[0]) {
        ShowSlice(0,playCounter[0]);
        playCounter[0]++;
      }
    }

    if (coronalPlay && coronal.activeInHierarchy) {
      if (sliceTimer >= playCounter[1] * playTick[1]) {
        ShowSlice(1,playCounter[1]);
        playCounter[1]++;
      }
    }

    if (sagittalPlay && sagittal.activeInHierarchy) {
      if (sliceTimer >= playCounter[2] * playTick[2]) {
        ShowSlice(2,playCounter[2]);
        playCounter[2]++;
      }
    }

    sliceTimer++;
	}



  FileInfo[] GetFilesFromFolder(string folder) {
    return (new DirectoryInfo(folder)).GetFiles("*.*");
  }

  FileInfo[] GetSortedImages(FileInfo[] files) {
    Array.Sort(files, CompareFiles);
    return files;
  }

  //CT file names require additional sorting instructions
  public static int CompareFiles(FileInfo f1, FileInfo f2) {
    string s1 = f1.Name;
    string s2 = f2.Name;

    bool b1 = s1.StartsWith("RT");
    bool b2 = s2.StartsWith("RT");
    if (b1 && !b2) {
      return 1;
    } else if (!b1 && b2) {
      return -1;
    } else if (b1 && b2) {
      return s1.CompareTo(s2);
    }

    string[] a1 = s1.Split('_');
    s1 = a1[a1.Length-1];
    s1 = s1.Replace(".dcm","");

    string[] a2 = s2.Split('_');
    s2 = a2[a2.Length-1];
    s2 = s2.Replace(".dcm","");

    return Int32.Parse(s1).CompareTo(Int32.Parse(s2));
  }

  public int[] GetDimensions(FileInfo file) {
    int[] d = new int[4];
    FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
    while (fs.Position < fs.Length && fs.Position >= 0) {
      string tag = GetNextTag(fs);
      int length = GetNextLength(fs);
      if (tag == ROWS) {
        d[0] = GetDataInt(fs,length);
      } else if (tag == COLS) {
        d[1] = GetDataInt(fs,length);
      } else if (tag == BITS) {
        d[2] = GetDataInt(fs,length);
      } else if (tag == BREP) {
        d[3] = GetDataInt(fs,length);
      } else {
        fs.Position += length;
      }
    }
    return d;
  }

  public int GetDataInt(FileStream fs, int length) {
    Byte[] bytes = new Byte[length];
    fs.Read(bytes,0,length);
    int ans = 0;
    for (int i = 0; i < length; i++) {
      ans += bytes[i] * hexMod[i];
    }
    return ans;
  }

  public string GetPixels(FileInfo file, ref int[,,] pixels, int z) {
    FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
    // BinaryReader r = new BinaryReader(fs);
    string ans = "";
    while (fs.Position < fs.Length && fs.Position >= 0) {
      string tag = GetNextTag(fs);
      int length = GetNextLength(fs);

      //Lowest Pixel Value for Slice
      if (tag == LPIX) {
        byte[] bytes = new byte[2];
        fs.Read(bytes,0,2);
        int lpix = BitConverter.ToInt16(bytes,0);
        sliceRange.Add(z,new int[]{lpix,0});
        continue;
      }

      //Highest Pixel Value for Slice
      if (tag == HPIX) {
        byte[] bytes = new byte[2];
        fs.Read(bytes,0,2);
        int hpix = BitConverter.ToInt16(bytes,0);
        int[] h = sliceRange[z];
        h[1] = hpix;
        sliceRange[z] = h;
        continue;
      }

      if (z == 0 && tag == PPOS) {
        //Image Coordinates
        byte[] bytes = new byte[length];
        fs.Read(bytes,0,length);
        String s = "";
        foreach (byte b in bytes) {
          s += (char)b;
        }
        ans = s;
        continue;
      }

      //Pixel Data
      if (tag == "7FE0,0010") {
        Byte[] b = new Byte[length];
        fs.Read(b, 0, length);
        pFull = 5000 + pHalf;
        int bCount = 0;

        for (int i = 0; i < x; i++) {
          for (int j = 0; j < y; j++) {
            pixels[j,i,z] = BitConverter.ToInt16(b,bCount);
            bCount += 2;
          }
        }
        break;
      }

      string data = "";
      if (length != 0) {
        data = GetData(fs, length);
      }
    }
    fs.Dispose();
    return ans;
  }

  void ShowSlice(string modelName, byte dir, int slice) {
    MeshMaker.Model model = models[modelName];

    int[,,] sliceData = slices[modelName];
    byte[,,] modelData = model.sliceData;
    int width = modelData.GetLength(0);
    int height = modelData.GetLength(1);
    int depth = modelData.GetLength(2);

    byte[] pix = null;
    Texture2D tex = null;
    int[] v = {0, width, 0, height, 0, depth};

    if (dir == 0) {
      if (slice >= depth) { slice = depth-1; }
      else if (slice < 0) { slice = 0; }
      pix = new byte[width*height*ARGB32];
      tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
      v[4] = slice;
      v[5] = slice+1;
      CopyPixels(modelData,sliceData,pix,v);
    }

    tex.LoadRawTextureData(pix);
    tex.Apply();
    if (dir == 0) {
      transverse.GetComponent<Renderer>().material.mainTexture = tex;
      // transverse.transform.position = new Vector3(positionalStart[0] + positionalStep[2] * slice, transverse.transform.position.y, transverse.transform.position.z);
    }




  }

  public void ShowSlice(byte dir, int slice) {
    byte[] pix = null;
    Texture2D tex = null;
    int[] v = {0, x, 0, y, 0, z};

    if (dir == 0) {
      if (slice >= z) { return; }
      //transverse - x,y
      pix = new byte[x*y*ARGB32];
      tex = new Texture2D(x, y, TextureFormat.ARGB32, false);
      v[4] = slice;
      v[5] = slice + 1;
      CopyPixels(pix,v);
    } else if (dir == 1) {
      if (slice >= y) { return; }
      //coronal - x,z
      pix = new byte[x*z*ARGB32];
      tex = new Texture2D(x, z, TextureFormat.ARGB32, false);
      v[2] = slice;
      v[3] = slice + 1;
      CopyPixels(pix,v);
    } else if (dir == 2) {
      if (slice >= x) { return; }
      //sagittal - y,z
      pix = new byte[y*z*ARGB32];
      tex = new Texture2D(y, z, TextureFormat.ARGB32, false);
      v[0] = slice;
      v[1] = slice + 1;
      CopyPixels(pix,v);
    }

    tex.LoadRawTextureData(pix);
    tex.Apply();
    if (dir == 0) {
      transverse.GetComponent<Renderer>().material.mainTexture = tex;
      transverse.transform.position = new Vector3(positionalStart[0] + positionalStep[2] * slice, transverse.transform.position.y, transverse.transform.position.z);
    } else if (dir == 1) {
      coronal.GetComponent<Renderer>().material.mainTexture = tex;
      coronal.transform.position = new Vector3(coronal.transform.position.x, positionalStart[1] + positionalStep[1] * slice, coronal.transform.position.z);
    } else if (dir == 2) {
      sagittal.GetComponent<Renderer>().material.mainTexture = tex;
      sagittal.transform.position = new Vector3(sagittal.transform.position.x, sagittal.transform.position.y, positionalStart[2] + positionalStep[0] * slice);
    }

  }

  public void CopyPixels(byte[,,] modelPixels, int[,,] slicePixels, byte[] pix, int[] v) {
    int count = 0;
    string s = "";
    for (int i = 0; i < v.Length; i++) {
      s += v[i];
      if ((i+1) % 2 == 0 && i != v.Length-1) {
        s+= " | ";
      }
    }

    print("CopyPixels: " + s);

    for (int x = v[0]; x < v[1]; x++) {
      for (int y = v[2]; y < v[3]; y++) {
        for (int z = v[4]; z < v[5]; z++) {
          byte a = 0x00;
          if (modelPixels[x,y,z] == 1) {
            a = 0xFF;
          }
          pix[0+count] = a;
          byte p = (byte)slicePixels[x,y,z];
          pix[1+count] = p;
          pix[2+count] = p;
          pix[3+count] = p;
          count += ARGB32;
        }
      }
    }
  }


  public void CopyPixels(byte[] pix, int[] v) {
    int count = 0;
    for (int i = v[0]; i < v[1]; i++) {
      for (int j = v[2]; j < v[3]; j++) {
        for (int k = v[4]; k < v[5]; k++) {
          byte p = ScaleInt(pixels[i,j,k], sliceRange[k]);
          pix[0+count] = 0xFF;
          for (int l = 1; l < ARGB32; l++) {
            pix[l+count] = p;
          }
          count += ARGB32;
        }
      }
    }
  }

  public byte ScaleInt(int i, int[] range) {
    int max = range[1] - range[0];
    int dist = max - i;
    float val = dist / (max+.0f);
    return (byte) Math.Round(255 * val);
  }

  /*
  //Raw
  //08 00 05 00
  //Becomes:
  //00 08 00 05
  //As a tag:
  //0008,0005
  */
  public string GetNextTag(FileStream fs) {
    Byte[] b = new Byte[4];
    fs.Read(b,0,4);
    return b[1].ToString("X2") + b[0].ToString("X2") + "," + b[3].ToString("X2") + b[2].ToString("X2");
  }

  // public string GetNextTag(BinaryReader r) {
    // Byte[] b = r.ReadBytes(4);
    // return b[1].ToString("X2") + b[0].ToString("X2") + "," + b[3].ToString("X2") + b[2].ToString("X2");
  // }

  /*
  //Raw
  //00 00 08 00
  //Becomes:
  //00 08 00 00
  */
  public int GetNextLength(FileStream fs) {
    Byte[] b = new Byte[4];
    fs.Read(b,0,4);
    return b[3] * hexMod[3] + b[2] * hexMod[2] + b[1] * hexMod[1] + b[0];
  }


  // public int GetNextLength(BinaryReader r) {
    // int[] i = r.ReadInts(4);
    // i[1] *= 16;
    // i[2] *= 256;
    // i[3] *= 4096;
    // return i[3] + i[2] + i[1] + i[0];

    // Byte[] b = r.ReadBytes(4);
    // return b[3] * hexMod3 + b[2] * hexMod2 + b[1] * hexMod1 + b[0];
  // }

  public string GetData(FileStream fs, int length) {
    Byte[] bytes = new Byte[length];
    fs.Read(bytes,0,length);
    string s = "";
    foreach (Byte b in bytes) {
      s += (char)b;
    }
    return s;
  }

  // public string GetData(BinaryReader r, int length) {
    //ReadChars is buggy, do not use it!
    //char[] c  = r.ReadChars(length);

    // Byte[] bytes = r.ReadBytes(length);
    // string s = "";
    // foreach (Byte b in bytes) {
      // s += (char)b;
    // }

    // return s;
  // }

  //Tags
  const string ROWS = "0028,0010";
  const string COLS = "0028,0011";
  const string BITS = "0028,0100";
  const string BREP = "0028,0103";
  const string LPIX = "0028,0106";
  const string HPIX = "0028,0107";
  const string PPOS = "0020,0032";

  //Structure Data
  const int S_GAP = 12;
  const int S_GAP_2 = 16;
  const string S_BLOCK_START = "FF FF FF FF FE FF 00 E0 FF FF FF FF";
  const string S_BLOCK_END   = "FE FF 0D E0 00 00 00 00 FE FF 00 E0 FF FF FF FF";
  const string S_BLOCK_END_2 = "FE FF 0D E0 00 00 00 00 FE FF DD E0 00 00 00 00";
  const string S_BLOCK_TAG = "FFFF,FFFF"; //+8
  const string S_BLOCK_TAG_2 = "FFFE,E00D"; //+12
  const string S_COL = "3006,002A";
  const string S_DATA = "3006,0050";

  //Structure Containers (Sequences)
  const string S_1 = "3006,0010"; //Referenced Frame of Reference Sequence
  const string S_2 = "3006,0012"; //RT Referenced Study Sequence
  const string S_3 = "3006,0014"; //RT Referenced Series Sequence
  const string S_4 = "3006,0016"; //Contour Image Seqeunce

  const string S_5 = "3006,0020"; //Structure Set ROI Sequence

  const string S_6 = "3006,0039"; //ROI Contour Sequence
  const string S_7 = "3006,0040"; //Contour Sequence

  const int ARGB32 = 4;



  /* Misc Tag Stuff

   /* Header */
      //---------
      /*(The Point of this block?)
      1. Block (3006,0010) S_1
        <Tags>
        1.1 Block (3006,0012) S_2
          <Tags>
          1.1.1 Block (3006,0014) S_3
            <Tags>
            1.1.1.1 Block (30006,0016) S_4
              <N Blocks>
            1.1.1.1 BlockEnd2
          1.1.1 BlockEnd2
        1.1 BlockEnd2
      1. BlockEnd2 */
      //---------
      /*(??)
      2. Block (3006,0020) S_5
        <N Blocks> */
      //---------
      /* (Contour + Colour Data)
      3. Block (3006,0039) S_6
        <Col Tag> (3006,002A)
        3.1 Block (3006,0040) S_7
          3.1.1 (3006,0016) S_4
          3.1.1 BlockEnd
          <Tags>
        3.1 <N Blocks>
        3.1 BlockEnd */
      //--------
      /* (??)
      4.

      */

      /* string tag = GetNextTag(fs);
      // if (tag != S_COL) {
        // continue;
      // } else {
        // int length = GetNextLength(fs);
        // print("Col Length: " + length + ", Position: " + fs.Position);
        // byte[] bytes = new byte[length];
        // fs.Read(bytes,0,length);
        // string data = "";
        // foreach (byte b in bytes) {
          // data += (char)b;
        // }
        // print("Col Data: " + data);
        // break;

      // }




      // string data = GetData(fs,length);
      */
}
