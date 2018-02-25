using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class LaserController : MonoBehaviour {


  public Transform isoCenter;

  

  HashSet<Material> mats = new HashSet<Material>();
  HashSet<Material> modelMats = new HashSet<Material>();
  int testCounter = 0;
  int testMax = 100;
  bool on = false;
  Vector3 bed;
  Vector3 linac;
  float radius = 0.005f;
  List<LaserTarget> laserTargets = new List<LaserTarget>();
  HashSet<string> mn = new HashSet<string>();
  int modelCount = 0;
  float[] lightIntensity;
  float dimLightIntensity = 0.2f;
  // float lerpCounter = 0;
  ObjectController oc;
  UIController uic;
  
  GameObject[] markers;
  Material markerMat;
  GameObject lastObj;
  MeshMaker.Model lastModel;

  public FileReader fileReader;
  public Light[] lights;


	// Use this for initialization
	void Start () {
    oc = GameObject.Find("Object Controller").GetComponent<ObjectController>();
    uic = GameObject.Find("UI Controller").GetComponent<UIController>();

    lightIntensity = new float[lights.Length];
    for (int i = 0; i < lights.Length; i++) {
      lightIntensity[i] = lights[i].intensity;
    }

    LaserTarget[] lsa = Transform.FindObjectsOfType(typeof (LaserTarget)) as LaserTarget[];
    foreach (LaserTarget ls in lsa) {
      laserTargets.Add(ls);
      mats.Add(ls.gameObject.GetComponent<MeshRenderer>().sharedMaterial);
    }
    AlignLasers(0);
	}

  void OnDestroy() {
    ChangeLasers(0);
  }


  


  List<MeshMaker.Model> UpdateLaserTargets() {
    GameObject currentModel = oc.GetCurrentObject();

    List<MeshMaker.Model> models = fileReader.GetModels();
    foreach (MeshMaker.Model m in models) {
      modelMats.Add(m.laserMat);
    }
    modelCount = models.Count;

    return models;
  }

  void UpdateModelLasers(List<MeshMaker.Model> models) {
    foreach (MeshMaker.Model m in models) {
      GameObject g0 = m.models[0];
      for (int i = 0; i < g0.transform.childCount; i++) {
        if (on) {
          g0.transform.GetChild(i).GetComponent<MeshRenderer>().material = m.laserMat;
        } else {
          g0.transform.GetChild(i).GetComponent<MeshRenderer>().material = m.mats[0];
        }
      }
    }
  }

  void SetLasers(int state) {
    foreach (Material m in mats) {
      m.SetInt("_ON", state);
    }
    foreach (Material m in modelMats) {
      m.SetInt("_ON", state);
    }
  }

  public void ToggleLasers() {
    List<MeshMaker.Model> models = UpdateLaserTargets();
    on = !on;
    AlignLasers(1);
    SetLasers(on ? 1 : 0);
    UpdateModelLasers(models);
    ToggleLights();
  }

  void ToggleLights() {
    for (int i = 0; i < lights.Length; i++) {
      lights[i].intensity = (on ? dimLightIntensity : lightIntensity[i]);
    }
  }

  public void ChangeLasers(float v) {
    if (Mathf.Round(v) == 0) {
      on = false;
      SetLasers(0);
    } else {
      on = true;
      SetLasers(1);
    }
  }

  void AlignLasers(int i = 0) {
    if (i == 0) {
      foreach (Material m in mats) {
        SetShaderCoords(m);
      }
    } else {
      foreach (Material m in modelMats) {
        SetShaderCoords(m);
      }
    }
  }

  void SetShaderCoords(Material m) {
    // print("Aligning: " + m);
    m.SetFloat("_xMin",isoCenter.position.x-radius);
    m.SetFloat("_xMax",isoCenter.position.x+radius);
    m.SetFloat("_yMin",isoCenter.position.y-radius);
    m.SetFloat("_yMax",isoCenter.position.y+radius);
    m.SetFloat("_zMin",isoCenter.position.z-radius);
    m.SetFloat("_zMax",isoCenter.position.z+radius);
  }

	// Update is called once per frame
	void FixedUpdate () {
    if (on) {      
      GameObject cur = oc.GetCurrentObject();
      MeshMaker.Model model;
      if (cur == null) { 
        UnityEngine.Debug.Log("No Obj");
        return;
      }
      if (lastObj != null && cur == lastObj && lastModel != null) {
        model = lastModel;
      } else {
        lastObj = cur;
        lastModel = model = fileReader.GetModel(cur);
      }
      
      if (cur == null || model == null || model.markers == null) { 
        UnityEngine.Debug.Log("No Obj/Model/Marker: " + model);
        return;
      }
      // UnityEngine.Debug.Log("Adjusting marker shader.");
      
      //Get position of markers
      Vector3 m1 = model.markers[0].transform.position;
      Vector3 m2 = model.markers[1].transform.position;
      
      //Update material with positions
      model.laserMat.SetFloat("_x1",m1.x);
      model.laserMat.SetFloat("_x2",m2.x);
      model.laserMat.SetFloat("_y1",m1.y);
      model.laserMat.SetFloat("_y2",m2.y);
      model.laserMat.SetFloat("_z1",m1.z);
      model.laserMat.SetFloat("_z2",m2.z);
      model.laserMat.SetInt("_ON_M",1);
      // UnityEngine.Debug.Log(m1 + " | " + m2);
      uic.UpdateIsoSign(model);
    } else {
      uic.UpdateIsoSign(null);
    }
	}
}
