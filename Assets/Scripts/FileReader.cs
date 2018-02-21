using UnityEngine;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System;
using System.Diagnostics;


public class FileReader : MonoBehaviour {

  //2^1, 2^8, 2^16, 2^24
  static int[] hexMod = {1,16*16,256*256,4096*4096};

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

  [Header("File Path")]
  public string pathToData;
  
  [Header("Slice Viewer")]
  public GameObject transverse;
  public GameObject coronal;
  public GameObject sagittal;
  public bool transversePlay;
  public bool coronalPlay;
  public bool sagittalPlay;

  [Header("Rendering")]
  public GameObject meshMarcher;
  public GameObject bed;
  public GameObject bedBackup;
  public GameObject bedSlot;
  public GameObject testBox;
  public bool drawMesh;
  [Range(0,20)]
  public int meshRangeMin = 0;
  [Range(0,20)]
  public int meshRangeMax = 1;
  [Range(-1,20)]
  public int meshRangeOverride = 0;
  

  static Thread mainThread = Thread.CurrentThread;
  
  public static bool printStopwatches = false;
  
  
  BedSlot bs;
  
  UIController uic;
  MarchingMeshCreator meshMarcherC;  
  Dictionary<string,MeshMaker.Model> models = new Dictionary<string,MeshMaker.Model>();
  Dictionary<string,int[,,]> slices = new Dictionary<string,int[,,]>();
  Dictionary<string,Vector3> slicePosition = new Dictionary<string,Vector3>();
  List<string> modelNames = new List<string>();  
  
  LinkedList<DoubleString> loadQueue = new LinkedList<DoubleString>();
  bool loadingModel = false;
  bool loadingModelDone = false;
  bool loadingModelChange = false;
  string loadingModelName = "";
  
  
  public static List<MeshMaker.ModelData> modelData;
  
  bool testMainThread = false;  
  
  struct DoubleString {
    public DoubleString(string s1, string s2) {
      this.s1 = s1;
      this.s2 = s2;
    }
    
    public string s1;
    public string s2;
  }

  string ModelNameGenerator(string filePath) {
    //Get folder name from path
    string fname = new DirectoryInfo(filePath).Name;
    
    int count = 0;
    while (models.ContainsKey(fname + " #" + count)) {
      count++;
    }
    fname += " #" + count;
    models.Add(fname,new MeshMaker.Model());
    return fname;    
  }

  public List<string> FindLoadableFolders() {
    //Could use default file path from a config file
    string defaultPath = "../..";
    if (pathToData != "") {
      defaultPath = pathToData;
    }    
    List<string> files = new List<string>();

    //To be loadable a folder must contain an RT structure file.
    foreach (string s in Directory.GetDirectories(defaultPath)) {
      FileInfo[] f = (new DirectoryInfo(s)).GetFiles("RT_*");
      if (f.Length != 0) {
        files.Add(s);
      }
    }
    return files;
  }

  void Start() {    
    bs = bedSlot.GetComponent<BedSlot>();
    uic = GameObject.Find("UI Controller").GetComponent<UIController>();
    // print(GameObject.Find("Wall").GetComponent<MeshRenderer>().material.shader.name);
    // Transform top = bed.transform.parent.parent;
    // Bounds bounds = new Bounds(top.position,Vector3.zero);
    // bounds.Encapsulate(bed.GetComponent<MeshRenderer>().bounds);
    // print("Bed: " + bounds);
    // return;

    for (int i = 0; i < 0; i++) {
      string s = "";
      // if (i % 2 == 0) {
        s = "./LUNG DICOM DATA";
      // } else {
        // s = "../../PELVIS DICOM DATA";
      // }
      LoadModel(s);      
    }
  }
  
/** ==============================================================================

    Creates a new thread and reads files from passed string (a folder).
    This data is then processed by various called methods, eventually
    producing mesh data via Marching Cubes.
    
    Parameter:
    filePath - name of folder that contains Dicom image and RT_Structure files.

  ============================================================================== **/
  public void LoadModel(string filePath) {
    //Generate unique name for new model made from passed file
    string fname = ModelNameGenerator(filePath);
    
    loadQueue.AddLast(new DoubleString(filePath,fname));
    loadingModelChange = true;
  }
  
	private void LoadModel (string filePath, string fname) {
    ModelLoader ml = new ModelLoader(filePath, fname, meshRangeOverride, meshRangeMin, meshRangeMax, drawMesh);
    ml.LoadModel();
	}
  
  public void RemoveAllModels() {
    while (modelNames.Count != 0) {
      RemoveModel(0);
    }
  }

  public void RemoveModel (int idx) {    
    string s = modelNames[idx];
    GameObject g = models[s].top;
    models.Remove(s);
    if (slices.ContainsKey(s)) { slices.Remove(s); }
    if (slicePosition.ContainsKey(s)) { slicePosition.Remove(s); }
    bs.CheckRemove(g);
    modelNames.RemoveAt(idx);
    Destroy(g);
  }

  //Retrieve a list of all currently loaded models
  public List<MeshMaker.Model> GetModels() {
    List<MeshMaker.Model> ret = new List<MeshMaker.Model>();
    foreach (string s in modelNames) {
      ret.Add(models[s]);
    }
    // print("Get Models: " + ret.Count);
    return ret;
  }


	/** ========================================================================
    
    Update is used primarily to trigger the next step of the model creation
    process. While the bulk of the work is done in non main threads. Work 
    with Unity classes like meshes, gameObjects, etc., must be done in the
    main thread.
    
    The next step is triggered whenever a non main thread sets a
    MeshMaker.ModelData collection to the static variable modelData.
    This also sets an event on the next update to correct the orientation
    of the produced model, which must be done on the next frame.
    
  
  ==========================================================================**/
	void Update () {
    if (rotate != null) {
      Transform t = null;
      if (bs.IsFull()) {
        t = bedBackup.transform;
      } else {
        t = bed.transform;
      }
      MeshMaker.ScaleModel(rotate,t,models[rotate.gameObject.name].dimensions[0]);
      rotate = null;
    }
    
    
    
    if (!loadingModel && loadQueue.Count > 0) {
      loadingModel = true;
      loadingModelDone = false;
      loadingModelChange = false;
      DoubleString ds = loadQueue.First.Value;
      loadQueue.RemoveFirst();
      
      //Avoid running this method on the Unity main thread.
      Thread t = new Thread(() => LoadModel(ds.s1, ds.s2));
      t.Start();
      
      loadingModelName = ds.s2;
      uic.UpdateLoadUI(ds.s2,loadQueue.Count);
    } else if (loadingModelChange) {
      loadingModelChange = false;
      uic.UpdateLoadUI(loadingModelName,loadQueue.Count);
    }

    if (FileReader.modelData != null) {      
      Stopwatch sw = new Stopwatch();
      sw.Start();
      List<MeshMaker.ModelData> mdl = FileReader.modelData;
      FileReader.modelData = null;
      MeshMaker.Model m = MeshMaker.CreateModel(mdl,mdl[0].name);
      models[m.name] = m;
      modelNames.Add(m.name);
      MeshMaker.FixPositions(models[m.name].dimensions,models[m.name].models[0].transform.parent.gameObject);
      rotate = models[m.name].models[0].transform.parent;
      uic.UpdateUI();
      // print("Slice");
      // ShowSlice(m.name,0,70);
      sw.Stop();
      printStopwatch(sw, "Update Model: ");
      loadingModelName = "";
      loadingModel = false;
      loadingModelChange = true;
    }
	}


  //Helper method for stopwatches, can be disabled by static global
  public static void printStopwatch(Stopwatch sw, string text) {
    if (!printStopwatches) { return; }
    TimeSpan ts = sw.Elapsed;
    string es = String.Format("{0:00}m {1:00}s {2:00}ms", ts.Minutes, ts.Seconds, ts.Milliseconds);
    UnityEngine.Debug.Log(text + es);
  }

  //Future work perhaps
  void LoadPlan(FileInfo f) {
    //#TODO
  }
  void LoadDose(FileInfo f) {
    //#TODO
  }

  public static FileInfo[] GetFilesFromFolder(string folder) {
    return (new DirectoryInfo(folder)).GetFiles("*.*");
  }

  public static FileInfo[] GetSortedImages(FileInfo[] files) {
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

  public static int[] GetDimensions(FileInfo file) {
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

  public static int GetDataInt(FileStream fs, int length) {
    Byte[] bytes = new Byte[length];
    fs.Read(bytes,0,length);
    int ans = 0;
    for (int i = 0; i < length; i++) {
      ans += bytes[i] * hexMod[i];
    }
    return ans;
  }

  public static string GetPixels(FileInfo file, ref int[,,] pixels, int x, int y, int z) {
    FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
    // BinaryReader r = new BinaryReader(fs);
    string ans = "";
    while (fs.Position < fs.Length && fs.Position >= 0) {
      string tag = GetNextTag(fs);
      int length = GetNextLength(fs);

      //Lowest Pixel Value for Slice
      // if (tag == LPIX) {
        // byte[] bytes = new byte[2];
        // fs.Read(bytes,0,2);
        // int lpix = BitConverter.ToInt16(bytes,0);
        // sliceRange.Add(z,new int[]{lpix,0});
        // continue;
      // }

      //Highest Pixel Value for Slice
      // if (tag == HPIX) {
        // byte[] bytes = new byte[2];
        // fs.Read(bytes,0,2);
        // int hpix = BitConverter.ToInt16(bytes,0);
        // int[] h = sliceRange[z];
        // h[1] = hpix;
        // sliceRange[z] = h;
        // continue;
      // }

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
        // pFull = 5000 + pHalf;
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
  public static string GetNextTag(FileStream fs) {
    Byte[] b = new Byte[4];
    fs.Read(b,0,4);
    return b[1].ToString("X2") + b[0].ToString("X2") + "," + b[3].ToString("X2") + b[2].ToString("X2");
  }
  
  public static int GetNextLength(FileStream fs) {
    Byte[] b = new Byte[4];
    fs.Read(b,0,4);
    return b[3] * hexMod[3] + b[2] * hexMod[2] + b[1] * hexMod[1] + b[0];
  }
  
  public static string GetData(FileStream fs, int length) {
    Byte[] bytes = new Byte[length];
    fs.Read(bytes,0,length);
    string s = "";
    foreach (Byte b in bytes) {
      s += (char)b;
    }
    return s;
  }

  //Tags
  public const string ROWS = "0028,0010";
  public const string COLS = "0028,0011";
  public const string BITS = "0028,0100";
  public const string BREP = "0028,0103";
  public const string LPIX = "0028,0106";
  public const string HPIX = "0028,0107";
  public const string PPOS = "0020,0032";

  //Structure Data
  public const int S_GAP = 12;
  public const int S_GAP_2 = 16;
  public const string S_BLOCK_START = "FF FF FF FF FE FF 00 E0 FF FF FF FF";
  public const string S_BLOCK_END   = "FE FF 0D E0 00 00 00 00 FE FF 00 E0 FF FF FF FF";
  public const string S_BLOCK_END_2 = "FE FF 0D E0 00 00 00 00 FE FF DD E0 00 00 00 00";
  public const string S_BLOCK_TAG = "FFFF,FFFF"; //+8
  public const string S_BLOCK_TAG_2 = "FFFE,E00D"; //+12
  public const string S_COL = "3006,002A";
  public const string S_DATA = "3006,0050";

  //Structure Containers (Sequences)
  public const string S_1 = "3006,0010"; //Referenced Frame of Reference Sequence
  public const string S_2 = "3006,0012"; //RT Referenced Study Sequence
  public const string S_3 = "3006,0014"; //RT Referenced Series Sequence
  public const string S_4 = "3006,0016"; //Contour Image Seqeunce

  public const string S_5 = "3006,0020"; //Structure Set ROI Sequence

  public const string S_6 = "3006,0039"; //ROI Contour Sequence
  public const string S_7 = "3006,0040"; //Contour Sequence

  public const int ARGB32 = 4;



  /* Misc Tag Info

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
