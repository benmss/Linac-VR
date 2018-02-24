using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Reflection;

public class ConfigController : MonoBehaviour {

  string cfgName = "LinacVR.cfg";
  List<string> setVars = new List<String>();

	// Use this for initialization
	void Start () {
		//Find config file, load values from it and apply these values to other scripts
    FileInfo file = new FileInfo(cfgName);

    if (!file.Exists) {
      print("Create Config");
      CreateConfigFile(file);
    } else {
      print("Load Config");
      LoadConfigFile(file);
    }
	}

  bool logged = false;

  void Update() {
    if (!logged && Logger.logger != null) {
      // foreach (string s in setVars) {
        // Logger.Log(s);
      // }
      Logger.Log("Loaded " + setVars.Count + " vars from config file.");
      logged = true;
    }
  }

  void CreateConfigFile(FileInfo file) {
    using (StreamWriter sw = file.CreateText()) {
      sw.WriteLine("# LinacVR configuration file. Comments start with a \"#\"");
      sw.WriteLine("# Non commented lines should be of the format <ScriptName>.<VariableName> = <value>");
      sw.WriteLine("# For example: FileReader.pathToData = ../modelData/");
      sw.WriteLine("# ==================================================");
      sw.WriteLine();

      sw.WriteLine("# FileReader");
      sw.WriteLine("FileReader.pathToData = '../Model Data/'");
      sw.WriteLine("FileReader.drawMesh = true");
      sw.WriteLine("FileReader.meshRangeMin = 0");
      sw.WriteLine("FileReader.meshRangeMax = 50");
      sw.WriteLine("FileReader.meshRangeOverride = -1");
      sw.WriteLine("FileReader.preloadModels = 1");
      sw.WriteLine("FileReader.preloadModelName = LUNG DICOM DATA");
      sw.WriteLine();

      sw.WriteLine("# UIController");
      sw.WriteLine("UIController.keyInput = false");
      sw.WriteLine();

      sw.WriteLine("# Logger");
      sw.WriteLine("Logger.logOutput = true");
      sw.WriteLine("Logger.logErrors = true");
    }
  }



  void LoadConfigFile(FileInfo file) {
    Dictionary<string,object> scriptMap = new Dictionary<string,object>();
    HashSet<string> blacklist = new HashSet<string>();
    GameObject[] topObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

    using (StreamReader sr = file.OpenText()) {
      while (sr.Peek() >= 0) {
        string line = sr.ReadLine();
        if (line == "") { continue; }
        if (line.StartsWith("#")) { continue; }

        string[] split1 = line.Split(new char[]{'.'},2);        
        if (split1 == null || split1.Length <= 1) { print("Split1 failed: " + line); continue; }
        
        string scriptName = split1[0];
        if (blacklist.Contains(scriptName)) { continue; }

        object comp = null;
        if (!scriptMap.ContainsKey(scriptName)) {
          //Find Object with this script
          for (int i = 0; i < topObjects.Length; i++) {
            comp = topObjects[i].GetComponent(scriptName);
            if (comp != null) {
              scriptMap.Add(scriptName, comp);
              break;
            }
          }
        } else {
          comp = scriptMap[scriptName];
        }

        if (comp == null) {
          //Failed to find script
          blacklist.Add(scriptName);
          print("Failed to find script: " + scriptName);
          continue;
        }

        //Field
        string[] split2 = split1[1].Split(new char[]{'='},2);
        if (split2 == null || split2.Length <= 1) { print("Split2 failed: " + split1[1]); continue; }
        string fieldName = split2[0].Trim();
        

        //Value
        //Try converting: Bool => Int => String
        //Add other types here if needed
        split2[1] = split2[1].Trim();
        object value = null;

        try { //Boolean
          bool b = Convert.ToBoolean(split2[1]);
          value = b;
        } catch (Exception e) {}

        if (value == null) {
          try { //Int
            int i = Convert.ToInt32(split2[1]);
            value = i;
          } catch (Exception e) {}
        }

        if (value == null) {
          //String
          value = split2[1];
        }


        bool ok = SetField(comp, fieldName, value);
        if (ok) { setVars.Add("ConfigController - Class: " + comp.GetType() + ", Field: " + fieldName + ", Value: " + value); }
        if (!ok) { print("SetField failed: " + comp + ", " + fieldName + ", " + value); }
      }
    }
    print("Loaded " + setVars.Count + " config variables.");
  }

  public static bool SetField(object inObj, string fieldName, object newValue) {
    FieldInfo info = inObj.GetType().GetField(fieldName);
    if (info != null) {
      info.SetValue(inObj, newValue);
      return true;
    } else {
      return false;
    }
  }



}
