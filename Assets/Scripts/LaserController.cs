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
  public FileReader fileReader;


	// Use this for initialization
	void Start () {
    LaserTarget[] lsa = Transform.FindObjectsOfType(typeof (LaserTarget)) as LaserTarget[];
    foreach (LaserTarget ls in lsa) {
      laserTargets.Add(ls);
      mats.Add(ls.gameObject.GetComponent<MeshRenderer>().sharedMaterial);
      // mn.Add(ls.gameObject.GetComponent<MeshRenderer>().material.name);
    }


    // foreach (string s in mn) {
      // print(s);
    // }

		// foreach (GameObject g in laserReceivers) {
      // if (g) {
        // mats.Add(g.GetComponent<Renderer>().material);
      // }
    // }

    // bed = bedAnchor.transform.position;
    // linac = linacAnchor.transform.position;
    AlignLasers(0);
	}

  void OnDestroy() {
    ChangeLasers(0);
  }



  List<MeshMaker.Model> UpdateLaserTargets() {
    List<MeshMaker.Model> models = fileReader.GetModels();
    // if (models.Count != modelCount) {
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
	void Update () {
		bool l = Input.GetKeyUp("l");
    bool t = Input.GetKeyUp("t");

    if (l) {
      on = !on;
      foreach (Material m in mats) {
        m.SetInt("_ON",(on == true ? 1 : 0));
      }
    }

    if (t && testCounter == -1) {
      testCounter = 0;
      foreach (Material m in mats) {
        m.SetInt("_OK",1);
      }
    }

    if (testCounter == testMax) {
      testCounter = -1;
      foreach (Material m in mats) {
        m.SetInt("_OK",0);
      }

    }

    if (testCounter >= 0) {
       testCounter++;
    }

	}
}
